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

        private static readonly Guid ClsidDirectMusicLoader = new("D2AC2892-B39B-11D1-8704-00600893B1BD");
        private static readonly Guid IidDirectMusicLoader8 = new("19E7C08C-0A44-4E6A-A116-595A7CD5DE8C");
        private static readonly Guid GuidDirectMusicAllTypes = new("D2AC2893-B39B-11D1-8704-00600893B1BD");

        private bool _disposed;
        private bool _driverLoaded;
        private bool _loaderPrepared;
        private bool _comInitialized;
        private IDirectMusicLoader8? _loaderComObject;

        internal Gedx8MusicDriverLoaderBackend(string? driverDirectory = null, int synthMode = 0, int preferredSampleRate = 0)
        {
            DriverDirectory = driverDirectory;
            SynthMode = synthMode;
            PreferredSampleRate = preferredSampleRate;
            AudioPathConfig = ResolveSynthConfig(synthMode, preferredSampleRate).Config;
            // _ = preferredSampleRate > 0 ? BuildSynthConfigForSampleRate(preferredSampleRate) : ResolveSynthConfig(SynthMode, PreferredSampleRate);
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

        internal int PreferredSampleRate { get; }

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
            Logger.Logger.Info("LoaderBackend", "CreateLoaderContext entered.");

            if (!EnsureDriverLoaded())
            {
                Logger.Logger.Error("LoaderBackend", "CreateLoaderContext aborted because EnsureDriverLoaded failed. LastError='" + (LastError ?? "<null>") + "'");
                return false;
            }

            if (_loaderPrepared)
            {
                Logger.Logger.Info("LoaderBackend", "Loader context already prepared.");
                return true;
            }

            if (string.IsNullOrWhiteSpace(SearchDirectory))
            {
                LastError = "DMManager: loader search directory missing.";
                Logger.Logger.Error("LoaderBackend", LastError);
                return false;
            }

            Logger.Logger.Info("LoaderBackend", "SearchDirectory='" + SearchDirectory + "'");
            Logger.Logger.Info("LoaderBackend", "CLSID_DirectMusicLoader=" + Logger.Logger.FormatGuid(ClsidDirectMusicLoader));
            Logger.Logger.Info("LoaderBackend", "IID_IDirectMusicLoader8=" + Logger.Logger.FormatGuid(IidDirectMusicLoader8));
            Logger.Logger.Info("LoaderBackend", "GUID_DirectMusicAllTypes=" + Logger.Logger.FormatGuid(GuidDirectMusicAllTypes));

            int coInitializeResult = NativeMethods.CoInitialize(IntPtr.Zero);
            Logger.Logger.Info("LoaderBackend", "CoInitialize returned " + Logger.Logger.FormatHResult(coInitializeResult));

            bool ownsComInitialization = coInitializeResult == 0 || coInitializeResult == 1;
            bool comAlreadyInitializedWithDifferentMode = coInitializeResult == unchecked((int)0x80010106);

            if (!ownsComInitialization && !comAlreadyInitializedWithDifferentMode)
            {
                LastError = "DMManager: CoInitialize failed. HRESULT=" + Logger.Logger.FormatHResult(coInitializeResult) + ".";
                Logger.Logger.Error("LoaderBackend", LastError);
                return false;
            }

            _comInitialized = ownsComInitialization;
            Logger.Logger.Info("LoaderBackend", "_comInitialized=" + _comInitialized);

            try
            {
                Type? loaderType = Type.GetTypeFromCLSID(ClsidDirectMusicLoader, throwOnError: false);
                Logger.Logger.Info("LoaderBackend", "Type.GetTypeFromCLSID => " + (loaderType == null ? "<null>" : loaderType.FullName ?? loaderType.Name));

                if (loaderType == null)
                {
                    LastError = "DMManager: DirectMusic loader COM class unavailable.";
                    Logger.Logger.Error("LoaderBackend", LastError);
                    CleanupComIfOwned();
                    return false;
                }

                object? comObject = Activator.CreateInstance(loaderType);
                Logger.Logger.Info("LoaderBackend", "Activator.CreateInstance => " + (comObject == null ? "<null>" : comObject.GetType().FullName ?? comObject.GetType().Name));

                if (comObject == null)
                {
                    LastError = "DMManager: failed to create DirectMusic loader COM object.";
                    Logger.Logger.Error("LoaderBackend", LastError);
                    CleanupComIfOwned();
                    return false;
                }

                IDirectMusicLoader8? loaderComObject = comObject as IDirectMusicLoader8;
                Logger.Logger.Info("LoaderBackend", "Direct cast to IDirectMusicLoader8 => " + (loaderComObject != null));

                if (loaderComObject == null)
                {
                    try
                    {
                        IntPtr iunknownPointer = Marshal.GetIUnknownForObject(comObject);
                        Logger.Logger.Info("LoaderBackend", "IUnknown pointer=" + Logger.Logger.FormatPointer(iunknownPointer));

                        try
                        {
                            Guid iidDirectMusicLoader8 = IidDirectMusicLoader8;
                            int hr = Marshal.QueryInterface(iunknownPointer, in iidDirectMusicLoader8, out IntPtr interfacePointer);
                            Logger.Logger.Info("LoaderBackend", "QueryInterface(IDirectMusicLoader8) hr=" + Logger.Logger.FormatHResult(hr) + ", interfacePointer=" + Logger.Logger.FormatPointer(interfacePointer));
                            Marshal.ThrowExceptionForHR(hr);

                            if (interfacePointer == IntPtr.Zero)
                            {
                                LastError = "DMManager: QueryInterface(IDirectMusicLoader8) returned null.";
                                Logger.Logger.Error("LoaderBackend", LastError);
                                CleanupComIfOwned();
                                return false;
                            }

                            try
                            {
                                loaderComObject = (IDirectMusicLoader8)Marshal.GetObjectForIUnknown(interfacePointer);
                                Logger.Logger.Info("LoaderBackend", "Marshal.GetObjectForIUnknown succeeded.");
                            }
                            finally
                            {
                                int releaseResult = Marshal.Release(interfacePointer);
                                Logger.Logger.Debug("LoaderBackend", "Release(interfacePointer) => refCount=" + releaseResult);
                            }
                        }
                        finally
                        {
                            int releaseResult = Marshal.Release(iunknownPointer);
                            Logger.Logger.Debug("LoaderBackend", "Release(iunknownPointer) => refCount=" + releaseResult);
                        }
                    }
                    catch (Exception ex)
                    {
                        LastError = "DMManager: DirectMusic loader QueryInterface failed. " + ex.Message;
                        Logger.Logger.Error("LoaderBackend", LastError, ex);
                        CleanupComIfOwned();
                        return false;
                    }
                }

                if (loaderComObject == null)
                {
                    LastError = "DMManager: IDirectMusicLoader8 unavailable.";
                    Logger.Logger.Error("LoaderBackend", LastError);
                    CleanupComIfOwned();
                    return false;
                }

                Guid directMusicAllTypes = GuidDirectMusicAllTypes;
                int setSearchDirectoryResult = loaderComObject.SetSearchDirectory(ref directMusicAllTypes, SearchDirectory, 0);
                Logger.Logger.Info("LoaderBackend", "SetSearchDirectory hr=" + Logger.Logger.FormatHResult(setSearchDirectoryResult) + ", path='" + SearchDirectory + "'");
                Marshal.ThrowExceptionForHR(setSearchDirectoryResult);

                _loaderComObject = loaderComObject;
                _loaderPrepared = true;
                Logger.Logger.Info("LoaderBackend", "CreateLoaderContext succeeded.");
                return true;
            }
            catch (COMException ex)
            {
                LastError = "DMManager: loader setup failed. HRESULT=0x" + ex.ErrorCode.ToString("X8") + ".";
                Logger.Logger.Error("LoaderBackend", LastError, ex);
                CleanupComIfOwned();
                return false;
            }
            catch (Exception ex)
            {
                LastError = "DMManager: loader setup failed. " + ex.Message;
                Logger.Logger.Error("LoaderBackend", LastError, ex);
                CleanupComIfOwned();
                return false;
            }
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

            InitSynthesizerDelegate initialize = Marshal.GetDelegateForFunctionPointer<InitSynthesizerDelegate>(functionPointer);

            InitSynthConfig synthConfig = ResolveSynthConfig(SynthMode, 0);

            Logger.Logger.Info(
                "LoaderBackend",
                "Initializing synthesizer with fixed mode mapping. SynthMode=" + SynthMode +
                ", SampleRate=" + synthConfig.SampleRate +
                ", Config=0x" + synthConfig.Config.ToString("X2"));

            byte result = initialize(DriverInstance, ref synthConfig);

            Logger.Logger.Info("LoaderBackend", "geInitSynthesizer result=" + result);

            if (result == 0)
            {
                LastError =
                    "DMManager: geInitSynthesizer failed for fixed mode mapping. " +
                    "SynthMode=" + SynthMode +
                    ", SampleRate=" + synthConfig.SampleRate +
                    ", Config=0x" + synthConfig.Config.ToString("X2") + ".";
                return false;
            }

            AudioPathConfig = synthConfig.Config;
            return true;
        }

        public void Shutdown()
        {
            Logger.Logger.Info("LoaderBackend", "Shutdown entered. _disposed=" + _disposed);

            if (_disposed)
            {
                Logger.Logger.Debug("LoaderBackend", "Shutdown skipped because backend is already disposed.");
                return;
            }

            if (MethodTable != IntPtr.Zero && DriverInstance != IntPtr.Zero)
            {
                Logger.Logger.Warning("LoaderBackend", "Calling experimental native shutdown chain for DriverInstance=" + Logger.Logger.FormatPointer(DriverInstance) + ". Offsets +0x30, +0x14 and +0x0C are still not RE-confirmed.");
                CallVoidWithInstance(MethodReleaseType2Object);
                CallVoidWithInstance(MethodShutdownLoader);
                CallVoidWithInstance(MethodShutdownDriver);
            }

            CleanupComIfOwned();

            if (MethodTable != IntPtr.Zero)
            {
                IntPtr releasePointer = ReadMethodPointer(MethodReleaseInterface);
                Logger.Logger.Info("LoaderBackend", "ReleaseInterface pointer=" + Logger.Logger.FormatPointer(releasePointer));

                if (releasePointer != IntPtr.Zero)
                {
                    ReleaseInterfaceDelegate release = Marshal.GetDelegateForFunctionPointer<ReleaseInterfaceDelegate>(releasePointer);
                    release();
                    Logger.Logger.Info("LoaderBackend", "ReleaseInterface called.");
                }
            }

            if (LibraryHandle != IntPtr.Zero)
            {
                bool freed = NativeMethods.FreeLibrary(LibraryHandle);
                Logger.Logger.Info("LoaderBackend", "FreeLibrary(" + Logger.Logger.FormatPointer(LibraryHandle) + ") => " + freed);
            }

            LibraryHandle = IntPtr.Zero;
            InterfaceObject = IntPtr.Zero;
            MethodTable = IntPtr.Zero;
            DriverInstance = IntPtr.Zero;
            _driverLoaded = false;
            _loaderPrepared = false;
            _disposed = true;

            Logger.Logger.Info("LoaderBackend", "Shutdown finished.");
        }

        public void Dispose()
        {
            Shutdown();
            GC.SuppressFinalize(this);
        }

        private void CleanupComIfOwned()
        {
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
        }

        private bool EnsureDriverLoaded()
        {
            Logger.Logger.Debug("LoaderBackend", "EnsureDriverLoaded entered. _disposed=" + _disposed + ", _driverLoaded=" + _driverLoaded);

            if (_disposed)
            {
                LastError = "DMManager: loader backend already disposed.";
                Logger.Logger.Error("LoaderBackend", LastError);
                return false;
            }

            if (_driverLoaded)
            {
                Logger.Logger.Debug("LoaderBackend", "Driver already loaded.");
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

            Logger.Logger.Info("LoaderBackend", "Loading driver DLL from '" + libraryPath + "'");

            LibraryHandle = NativeMethods.LoadLibrary(libraryPath);
            Logger.Logger.Info("LoaderBackend", "LoadLibrary => " + Logger.Logger.FormatPointer(LibraryHandle));

            if (LibraryHandle == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                LastError = "DMManager: LoadLibrary failed. Path='" + libraryPath + "', Win32Error=" + error;
                Logger.Logger.Error("LoaderBackend", LastError);
                return false;
            }

            IntPtr getInterfacePointer = NativeMethods.GetProcAddress(LibraryHandle, DriverExportName);
            Logger.Logger.Info("LoaderBackend", "GetProcAddress('" + DriverExportName + "') => " + Logger.Logger.FormatPointer(getInterfacePointer));

            if (getInterfacePointer == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                LastError = "DMManager: GetProcAddress failed. Export='" + DriverExportName + "', Win32Error=" + error;
                Logger.Logger.Error("LoaderBackend", LastError);
                NativeMethods.FreeLibrary(LibraryHandle);
                LibraryHandle = IntPtr.Zero;
                return false;
            }

            GetInterface2Delegate getInterface2 = Marshal.GetDelegateForFunctionPointer<GetInterface2Delegate>(getInterfacePointer);
            byte ok = getInterface2(out IntPtr interfaceObject);
            Logger.Logger.Info("LoaderBackend", "GetInterface2 => ok=" + ok + ", interfaceObject=" + Logger.Logger.FormatPointer(interfaceObject));

            if (ok == 0 || interfaceObject == IntPtr.Zero)
            {
                LastError = "DMManager: GetInterface2 returned null interface object.";
                Logger.Logger.Error("LoaderBackend", LastError);
                NativeMethods.FreeLibrary(LibraryHandle);
                LibraryHandle = IntPtr.Zero;
                return false;
            }

            InterfaceObject = interfaceObject;
            MethodTable = Marshal.ReadIntPtr(InterfaceObject, 4);

            Logger.Logger.Info("LoaderBackend", "InterfaceObject=" + Logger.Logger.FormatPointer(InterfaceObject) + ", MethodTable=" + Logger.Logger.FormatPointer(MethodTable));

            if (MethodTable == IntPtr.Zero)
            {
                LastError = "DMManager: interface object has null method table.";
                Logger.Logger.Error("LoaderBackend", LastError);
                NativeMethods.FreeLibrary(LibraryHandle);
                LibraryHandle = IntPtr.Zero;
                InterfaceObject = IntPtr.Zero;
                return false;
            }

            Logger.Logger.Info("LoaderBackend", "Method +00=" + Logger.Logger.FormatPointer(ReadMethodPointer(MethodBootstrap)));
            Logger.Logger.Info("LoaderBackend", "Method +04=" + Logger.Logger.FormatPointer(ReadMethodPointer(MethodReleaseInterface)));
            Logger.Logger.Info("LoaderBackend", "Method +08=" + Logger.Logger.FormatPointer(ReadMethodPointer(MethodCreateInstance)));
            Logger.Logger.Info("LoaderBackend", "Method +0C=" + Logger.Logger.FormatPointer(ReadMethodPointer(MethodShutdownDriver)));
            Logger.Logger.Info("LoaderBackend", "Method +10=" + Logger.Logger.FormatPointer(ReadMethodPointer(MethodInitSynthesizer)));
            Logger.Logger.Info("LoaderBackend", "Method +14=" + Logger.Logger.FormatPointer(ReadMethodPointer(MethodShutdownLoader)));
            Logger.Logger.Info("LoaderBackend", "Method +30=" + Logger.Logger.FormatPointer(ReadMethodPointer(MethodReleaseType2Object)));

            IntPtr bootstrapPointer = ReadMethodPointer(MethodBootstrap);
            if (bootstrapPointer != IntPtr.Zero)
            {
                Logger.Logger.Info("LoaderBackend", "Calling bootstrap.");
                VoidNoArgsDelegate bootstrap = Marshal.GetDelegateForFunctionPointer<VoidNoArgsDelegate>(bootstrapPointer);
                bootstrap();
                Logger.Logger.Info("LoaderBackend", "Bootstrap returned.");
            }

            IntPtr createInstancePointer = ReadMethodPointer(MethodCreateInstance);
            Logger.Logger.Info("LoaderBackend", "CreateInstance pointer=" + Logger.Logger.FormatPointer(createInstancePointer));

            if (createInstancePointer == IntPtr.Zero)
            {
                LastError = "DMManager: geCreateInstance method missing.";
                Logger.Logger.Error("LoaderBackend", LastError);
                NativeMethods.FreeLibrary(LibraryHandle);
                LibraryHandle = IntPtr.Zero;
                InterfaceObject = IntPtr.Zero;
                MethodTable = IntPtr.Zero;
                return false;
            }

            CreateInstanceDelegate createInstance = Marshal.GetDelegateForFunctionPointer<CreateInstanceDelegate>(createInstancePointer);
            DriverInstance = createInstance();

            Logger.Logger.Info("LoaderBackend", "DriverInstance=" + Logger.Logger.FormatPointer(DriverInstance));

            if (DriverInstance == IntPtr.Zero)
            {
                LastError = "DMManager: geCreateInstance failed.";
                Logger.Logger.Error("LoaderBackend", LastError);
                NativeMethods.FreeLibrary(LibraryHandle);
                LibraryHandle = IntPtr.Zero;
                InterfaceObject = IntPtr.Zero;
                MethodTable = IntPtr.Zero;
                return false;
            }

            _driverLoaded = true;
            _disposed = false;
            Logger.Logger.Info("LoaderBackend", "EnsureDriverLoaded succeeded.");
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

        private static InitSynthConfig ResolveSynthConfig(int synthMode, int fallbackSampleRate)
        {
            return synthMode switch
            {
                1 => new InitSynthConfig
                {
                    Reserved00 = 0,
                    SampleRate = 22050,
                    Config = 0x10,
                },
                2 => new InitSynthConfig
                {
                    Reserved00 = 0,
                    SampleRate = 11025,
                    Config = 0x08,
                },
                _ => new InitSynthConfig
                {
                    Reserved00 = 0,
                    SampleRate = 44100,
                    Config = 0x40,
                },
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
