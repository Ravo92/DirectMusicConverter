using System.Runtime.InteropServices;
using DirectMusicConverter.Interfaces;

namespace DirectMusicConverter.Classes
{
    internal sealed class Gedx8MusicDriverLoaderBackend : IDmLoaderBackend, IDisposable
    {
        private const string DriverDllName = "gedx8musicdrv.dll";
        private const string DriverExportName = "GetInterface2";
        private const int MethodBootstrapDriver = 0x00;
        private const int MethodReleaseInterface = 0x04;
        private const int MethodCreateInstance = 0x08;
        private const int MethodInitSynthesizer = 0x10;

        private readonly int _initMode;
        private bool _disposed;
        private bool _driverLoaded;
        private bool _performanceCreated;
        private bool _composerCreated;
        private bool _instanceCreated;
        private bool _synthInitialized;

        internal Gedx8MusicDriverLoaderBackend(string? driverDirectory = null, int initMode = 0)
        {
            DriverDirectory = driverDirectory;
            _initMode = initMode;

            DmSynthInitConfig config = CreateSynthInitConfig(initMode);
            SampleRate = config.SampleRate;
            AudiopathConfig = config.Config;
        }

        internal string? DriverDirectory { get; }

        internal string? LastError { get; private set; }

        internal IntPtr LibraryHandle { get; private set; }

        internal IntPtr InterfaceObject { get; private set; }

        internal IntPtr MethodTable { get; private set; }

        internal IntPtr DriverInstance { get; private set; }

        internal IntPtr LoaderContext { get; private set; }

        internal int SampleRate { get; private set; }

        internal int AudiopathConfig { get; private set; }

        public bool CreatePerformance()
        {
            if (!EnsureDriverLoaded())
            {
                return false;
            }

            if (_performanceCreated)
            {
                return true;
            }

            IntPtr functionPointer = ReadMethodPointer(MethodBootstrapDriver);
            if (functionPointer == IntPtr.Zero)
            {
                LastError = "DMManager: bootstrap method missing at +0x00.";
                return false;
            }

            BootstrapDriverDelegate bootstrap = Marshal.GetDelegateForFunctionPointer<BootstrapDriverDelegate>(functionPointer);
            bootstrap();

            _performanceCreated = true;
            return true;
        }

        public bool CreateComposer()
        {
            if (!EnsureDriverLoaded())
            {
                return false;
            }

            _composerCreated = true;
            return true;
        }

        public bool CreateLoaderContext()
        {
            if (!EnsureDriverLoaded())
            {
                return false;
            }

            if (LoaderContext != IntPtr.Zero)
            {
                return true;
            }

            LastError = "DMManager: loader context creation is still unresolved. +0x08 is geCreateInstance, not geCreateContext.";
            return false;
        }

        public bool InitializeSynthesizer()
        {
            if (!EnsureDriverLoaded())
            {
                return false;
            }

            if (_synthInitialized)
            {
                return true;
            }

            if (!CreatePerformance())
            {
                return false;
            }

            if (!EnsureDriverInstanceCreated())
            {
                return false;
            }

            IntPtr functionPointer = ReadMethodPointer(MethodInitSynthesizer);
            if (functionPointer == IntPtr.Zero)
            {
                LastError = "DMManager: geInitSynthesizer method missing at +0x10.";
                return false;
            }

            DmSynthInitConfig config = CreateSynthInitConfig(_initMode);
            InitSynthesizerDelegate initialize = Marshal.GetDelegateForFunctionPointer<InitSynthesizerDelegate>(functionPointer);
            byte result = initialize(DriverInstance, ref config);
            if (result == 0)
            {
                LastError = "DMManager: geInitSynthesizer failed. DriverInstance=0x" + DriverInstance.ToInt64().ToString("X") + ", SampleRate=" + config.SampleRate + ", Config=0x" + config.Config.ToString("X");
                return false;
            }

            SampleRate = config.SampleRate;
            AudiopathConfig = config.Config;
            _synthInitialized = true;
            return true;
        }

        public void Shutdown()
        {
            if (_disposed)
            {
                return;
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
            LoaderContext = IntPtr.Zero;
            _driverLoaded = false;
            _performanceCreated = false;
            _composerCreated = false;
            _instanceCreated = false;
            _synthInitialized = false;
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
            getInterface2(out IntPtr interfaceObject);

            if (interfaceObject == IntPtr.Zero)
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
                LastError = "DMManager: interface object contains a null method table pointer at +0x04.";
                NativeMethods.FreeLibrary(LibraryHandle);
                LibraryHandle = IntPtr.Zero;
                InterfaceObject = IntPtr.Zero;
                return false;
            }

            _driverLoaded = true;
            _disposed = false;
            return true;
        }

        private bool EnsureDriverInstanceCreated()
        {
            if (DriverInstance != IntPtr.Zero)
            {
                _instanceCreated = true;
                return true;
            }

            IntPtr functionPointer = ReadMethodPointer(MethodCreateInstance);
            if (functionPointer == IntPtr.Zero)
            {
                LastError = "DMManager: geCreateInstance method missing at +0x08.";
                return false;
            }

            CreateInstanceDelegate createInstance = Marshal.GetDelegateForFunctionPointer<CreateInstanceDelegate>(functionPointer);
            DriverInstance = createInstance();
            if (DriverInstance == IntPtr.Zero)
            {
                LastError = "DMManager: geCreateInstance failed.";
                return false;
            }

            _instanceCreated = true;
            return true;
        }

        private IntPtr ReadMethodPointer(int offset)
        {
            if (MethodTable == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            return Marshal.ReadIntPtr(MethodTable, offset);
        }

        private static DmSynthInitConfig CreateSynthInitConfig(int initMode)
        {
            DmSynthInitConfig config = new()
            {
                Reserved00 = 0,
                SampleRate = 44100,
                Config = 0x40,
            };

            if (initMode == 1)
            {
                config.SampleRate = 22050;
                config.Config = 0x10;
                return config;
            }

            if (initMode == 2)
            {
                config.SampleRate = 11025;
                config.Config = 0x08;
                return config;
            }

            return config;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DmSynthInitConfig
        {
            internal int Reserved00;
            internal int SampleRate;
            internal int Config;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void GetInterface2Delegate(out IntPtr interfaceObject);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void BootstrapDriverDelegate();

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate IntPtr CreateInstanceDelegate();

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate byte InitSynthesizerDelegate(IntPtr driverInstance, ref DmSynthInitConfig config);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void ReleaseInterfaceDelegate();
    }
}
