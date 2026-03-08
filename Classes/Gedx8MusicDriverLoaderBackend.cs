using System.Runtime.InteropServices;
using DirectMusicConverter.Interfaces;

namespace DirectMusicConverter.Classes
{
    internal sealed class Gedx8MusicDriverLoaderBackend : IDmLoaderBackend, IDisposable
    {
        private const string DriverDllName = "gedx8musicdrv.dll";
        private const string DriverExportName = "GetInterface2";
        private const int MethodBootstrap = 0x00;
        private const int MethodReleaseInterface = 0x04;
        private const int MethodCreateInstance = 0x08;
        private const int MethodShutdownDriver = 0x0C;
        private const int MethodInitSynthesizer = 0x10;
        private const int MethodShutdownLoader = 0x14;
        private const int MethodReleaseType2Object = 0x30;

        private static readonly Guid ClsidDirectMusicLoader = new("D2AC2881-B39B-11D1-8704-00600893B1BD");
        private static readonly Guid IidDirectMusicLoader8 = new("19E7C679-0A44-4E6A-A116-585A7CD5DE8C");
        private static readonly Guid GuidDirectMusicAllTypes = new("D2AC2890-B39B-11D1-8704-00600893B1BD");

        private bool _disposed;
        private bool _driverLoaded;
        private bool _loaderPrepared;
        private bool _comInitialized;
        private IDirectMusicLoader8? _loaderComObject;

        internal Gedx8MusicDriverLoaderBackend(string? driverDirectory = null, int synthMode = 0)
        {
            DriverDirectory = driverDirectory;
            SynthMode = synthMode;
            AudioPathConfig = ResolveSynthConfig(synthMode).Config;
        }

        internal string? DriverDirectory { get; }

        internal int SynthMode { get; }

        internal string? SearchDirectory { get; private set; }

        internal string? LastError { get; private set; }

        internal IntPtr LibraryHandle { get; private set; }

        internal IntPtr InterfaceObject { get; private set; }

        internal IntPtr MethodTable { get; private set; }

        internal IntPtr DriverInstance { get; private set; }

        internal int AudioPathConfig { get; private set; }

        internal void SetSearchDirectory(string? searchDirectory)
        {
            SearchDirectory = searchDirectory;
        }

        public bool CreatePerformance()
        {
            return EnsureDriverLoaded();
        }

        public bool CreateComposer()
        {
            return EnsureDriverLoaded();
        }

        public bool CreateLoaderContext()
        {
            if (!EnsureDriverLoaded())
            {
                return false;
            }

            if (_loaderPrepared)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(SearchDirectory))
            {
                LastError = "DMManager: loader search directory missing.";
                return false;
            }

            int coInitializeResult = NativeMethods.CoInitialize(IntPtr.Zero);

            bool ownsComInitialization = coInitializeResult == 0 || coInitializeResult == 1;
            bool comAlreadyInitializedWithDifferentMode = coInitializeResult == unchecked((int)0x80010106);

            if (!ownsComInitialization && !comAlreadyInitializedWithDifferentMode)
            {
                LastError = "DMManager: CoInitialize failed. HRESULT=0x" + coInitializeResult.ToString("X8") + ".";
                return false;
            }

            _comInitialized = ownsComInitialization;

            Type? loaderType = Type.GetTypeFromCLSID(ClsidDirectMusicLoader, throwOnError: false);
            if (loaderType == null)
            {
                if (_comInitialized)
                {
                    NativeMethods.CoUninitialize();
                    _comInitialized = false;
                }

                LastError = "DMManager: DirectMusic loader COM class unavailable.";
                return false;
            }

            object? comObject = Activator.CreateInstance(loaderType);
            if (comObject == null)
            {
                if (_comInitialized)
                {
                    NativeMethods.CoUninitialize();
                    _comInitialized = false;
                }

                LastError = "DMManager: failed to create DirectMusic loader COM object.";
                return false;
            }

            try
            {
                IntPtr iunknownPointer = Marshal.GetIUnknownForObject(comObject);
                try
                {
                    Guid iidDirectMusicLoader8 = IidDirectMusicLoader8;
                    int hr = Marshal.QueryInterface(iunknownPointer, ref iidDirectMusicLoader8, out IntPtr interfacePointer);
                    Marshal.ThrowExceptionForHR(hr);

                    if (interfacePointer == IntPtr.Zero)
                    {
                        LastError = "DMManager: QueryInterface(IDirectMusicLoader8) returned null.";
                        return false;
                    }

                    try
                    {
                        _loaderComObject = (IDirectMusicLoader8)Marshal.GetTypedObjectForIUnknown(interfacePointer, typeof(IDirectMusicLoader8));
                    }
                    finally
                    {
                        Marshal.Release(interfacePointer);
                    }
                }
                finally
                {
                    Marshal.Release(iunknownPointer);
                }
            }
            catch (Exception ex)
            {
                LastError = "DMManager: DirectMusic loader QueryInterface failed. " + ex.Message;
                return false;
            }

            if (_loaderComObject == null)
            {
                LastError = "DMManager: IDirectMusicLoader8 unavailable.";
                return false;
            }

            try
            {
                Guid directMusicAllTypes = GuidDirectMusicAllTypes;
                int hr = _loaderComObject.SetSearchDirectory(ref directMusicAllTypes, SearchDirectory, 0);
                Marshal.ThrowExceptionForHR(hr);
            }
            catch (COMException ex)
            {
                LastError = "DMManager: IDirectMusicLoader8::SetSearchDirectory failed. HRESULT=0x" + ex.ErrorCode.ToString("X8") + ".";
                return false;
            }

            _loaderPrepared = true;
            return true;
        }

        public bool InitializeSynthesizer()
        {
            if (!EnsureDriverLoaded())
            {
                return false;
            }

            IntPtr functionPointer = ReadMethodPointer(MethodInitSynthesizer);
            if (functionPointer == IntPtr.Zero)
            {
                LastError = "DMManager: geInitSynthesizer method missing.";
                return false;
            }

            InitSynthConfig synthConfig = ResolveSynthConfig(SynthMode);
            AudioPathConfig = synthConfig.Config;

            InitSynthesizerDelegate initialize = Marshal.GetDelegateForFunctionPointer<InitSynthesizerDelegate>(functionPointer);
            byte result = initialize(DriverInstance, ref synthConfig);
            if (result == 0)
            {
                LastError = "DMManager: geInitSynthesizer failed.";
                return false;
            }

            return true;
        }

        public void Shutdown()
        {
            if (_disposed)
            {
                return;
            }

            if (MethodTable != IntPtr.Zero && DriverInstance != IntPtr.Zero)
            {
                CallVoidWithInstance(MethodReleaseType2Object);
                CallVoidWithInstance(MethodShutdownLoader);
                CallVoidWithInstance(MethodShutdownDriver);
            }

            if (_loaderComObject != null)
            {
                Marshal.ReleaseComObject(_loaderComObject);
                _loaderComObject = null;
            }

            if (_comInitialized)
            {
                NativeMethods.CoUninitialize();
                _comInitialized = false;
            }

            if (MethodTable != IntPtr.Zero)
            {
                IntPtr releasePointer = ReadMethodPointer(MethodReleaseInterface);
                if (releasePointer != IntPtr.Zero)
                {
                    ReleaseInterfaceDelegate release = Marshal.GetDelegateForFunctionPointer<ReleaseInterfaceDelegate>(releasePointer);
                    release();
                }
            }

            if (LibraryHandle != IntPtr.Zero)
            {
                NativeMethods.FreeLibrary(LibraryHandle);
            }

            LibraryHandle = IntPtr.Zero;
            InterfaceObject = IntPtr.Zero;
            MethodTable = IntPtr.Zero;
            DriverInstance = IntPtr.Zero;
            _driverLoaded = false;
            _loaderPrepared = false;
            _disposed = true;
        }

        public void Dispose()
        {
            Shutdown();
            GC.SuppressFinalize(this);
        }

        private bool EnsureDriverLoaded()
        {
            if (_disposed)
            {
                LastError = "DMManager: loader backend already disposed.";
                return false;
            }

            if (_driverLoaded)
            {
                return true;
            }

            string libraryPath = DriverDllName;
            if (!string.IsNullOrWhiteSpace(DriverDirectory))
            {
                string candidate = Path.Combine(DriverDirectory, DriverDllName);
                if (File.Exists(candidate))
                {
                    libraryPath = candidate;
                }
            }

            LibraryHandle = NativeMethods.LoadLibrary(libraryPath);
            if (LibraryHandle == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                LastError = "DMManager: LoadLibrary failed. Path='" + libraryPath + "', Win32Error=" + error;
                return false;
            }

            IntPtr getInterfacePointer = NativeMethods.GetProcAddress(LibraryHandle, DriverExportName);
            if (getInterfacePointer == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                LastError = "DMManager: GetProcAddress failed. Export='" + DriverExportName + "', Win32Error=" + error;
                NativeMethods.FreeLibrary(LibraryHandle);
                LibraryHandle = IntPtr.Zero;
                return false;
            }

            GetInterface2Delegate getInterface2 = Marshal.GetDelegateForFunctionPointer<GetInterface2Delegate>(getInterfacePointer);
            byte ok = getInterface2(out IntPtr interfaceObject);
            if (ok == 0 || interfaceObject == IntPtr.Zero)
            {
                LastError = "DMManager: GetInterface2 returned null interface object.";
                NativeMethods.FreeLibrary(LibraryHandle);
                LibraryHandle = IntPtr.Zero;
                return false;
            }

            InterfaceObject = interfaceObject;
            MethodTable = Marshal.ReadIntPtr(InterfaceObject, 4);
            if (MethodTable == IntPtr.Zero)
            {
                LastError = "DMManager: interface object has null method table.";
                NativeMethods.FreeLibrary(LibraryHandle);
                LibraryHandle = IntPtr.Zero;
                InterfaceObject = IntPtr.Zero;
                return false;
            }

            IntPtr bootstrapPointer = ReadMethodPointer(MethodBootstrap);
            if (bootstrapPointer != IntPtr.Zero)
            {
                VoidNoArgsDelegate bootstrap = Marshal.GetDelegateForFunctionPointer<VoidNoArgsDelegate>(bootstrapPointer);
                bootstrap();
            }

            IntPtr createInstancePointer = ReadMethodPointer(MethodCreateInstance);
            if (createInstancePointer == IntPtr.Zero)
            {
                LastError = "DMManager: geCreateInstance method missing.";
                NativeMethods.FreeLibrary(LibraryHandle);
                LibraryHandle = IntPtr.Zero;
                InterfaceObject = IntPtr.Zero;
                MethodTable = IntPtr.Zero;
                return false;
            }

            CreateInstanceDelegate createInstance = Marshal.GetDelegateForFunctionPointer<CreateInstanceDelegate>(createInstancePointer);
            DriverInstance = createInstance();
            if (DriverInstance == IntPtr.Zero)
            {
                LastError = "DMManager: geCreateInstance failed.";
                NativeMethods.FreeLibrary(LibraryHandle);
                LibraryHandle = IntPtr.Zero;
                InterfaceObject = IntPtr.Zero;
                MethodTable = IntPtr.Zero;
                return false;
            }

            _driverLoaded = true;
            _disposed = false;
            return true;
        }

        private void CallVoidWithInstance(int offset)
        {
            IntPtr functionPointer = ReadMethodPointer(offset);
            if (functionPointer == IntPtr.Zero || DriverInstance == IntPtr.Zero)
            {
                return;
            }

            VoidWithInstanceDelegate method = Marshal.GetDelegateForFunctionPointer<VoidWithInstanceDelegate>(functionPointer);
            method(DriverInstance);
        }

        private IntPtr ReadMethodPointer(int offset)
        {
            if (MethodTable == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            return Marshal.ReadIntPtr(MethodTable, offset);
        }

        private static InitSynthConfig ResolveSynthConfig(int synthMode)
        {
            return synthMode switch
            {
                1 => new InitSynthConfig { Reserved00 = 0, SampleRate = 22050, Config = 0x10 },
                2 => new InitSynthConfig { Reserved00 = 0, SampleRate = 11025, Config = 0x08 },
                _ => new InitSynthConfig { Reserved00 = 0, SampleRate = 44100, Config = 0x40 },
            };
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct InitSynthConfig
        {
            internal int Reserved00;
            internal int SampleRate;
            internal int Config;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate byte GetInterface2Delegate(out IntPtr interfaceObject);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void VoidNoArgsDelegate();

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate IntPtr CreateInstanceDelegate();

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate byte InitSynthesizerDelegate(IntPtr driverInstance, ref InitSynthConfig config);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void VoidWithInstanceDelegate(IntPtr driverInstance);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void ReleaseInterfaceDelegate();
    }
}
