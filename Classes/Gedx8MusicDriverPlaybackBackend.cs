using System.Runtime.InteropServices;
using DirectMusicConverter.Interfaces;

namespace DirectMusicConverter.Classes
{
    internal sealed class Gedx8MusicDriverPlaybackBackend : IDmPlaybackBackend
    {
        private const int MethodLoadCachedObject = 0x20;
        private const int MethodCreateAudiopath = 0x34;
        private const int MethodActivateAudiopath = 0x38;
        private const int MethodDestroyAudiopath = 0x50;
        private const int MethodSetVolumeOfAudiopath = 0x3C;
        private const int MethodStartSegmentPlayback = 0x54;
        private const int MethodResetSegmentPlayback = 0x58;
        private const int MethodGetPlaybackStateOfSegment = 0x5C;
        private const int MethodDestroySegment = 0x6C;

        private readonly Gedx8MusicDriverLoaderBackend _loaderBackend;

        internal Gedx8MusicDriverPlaybackBackend(Gedx8MusicDriverLoaderBackend loaderBackend)
        {
            _loaderBackend = loaderBackend;
        }

        internal string? LastError { get; private set; }

        public bool ActivateAudiopath(object? audiopath, bool active)
        {
            IntPtr audiopathPointer = UnwrapHandle(audiopath);
            if (audiopathPointer == IntPtr.Zero)
            {
                return false;
            }

            ActivateAudiopathDelegate method = GetMethod<ActivateAudiopathDelegate>(MethodActivateAudiopath);
            if (method == null)
            {
                return false;
            }

            byte result = method(_loaderBackend.DriverInstance, audiopathPointer, active ? 1 : 0);
            return result != 0;
        }

        public bool CreateAudiopath(int audiopathMode, int config, object? segmentHandle, out object? audiopath)
        {
            audiopath = null;

            CreateAudiopathDelegate method = GetMethod<CreateAudiopathDelegate>(MethodCreateAudiopath);
            if (method == null)
            {
                return false;
            }

            DmAudiopathConfig nativeConfig = new DmAudiopathConfig
            {
                Mode = audiopathMode,
                Config = config,
                Reserved08 = 0,
                Reserved0C = 0,
            };

            IntPtr outAudiopath = IntPtr.Zero;
            IntPtr segmentPointer = UnwrapHandle(segmentHandle);
            byte result = method(_loaderBackend.DriverInstance, ref nativeConfig, out outAudiopath, segmentPointer);
            if (result == 0)
            {
                return false;
            }

            audiopath = new NativeHandle(outAudiopath);
            return true;
        }

        public bool DestroyAudiopath(object? audiopath)
        {
            IntPtr audiopathPointer = UnwrapHandle(audiopath);
            if (audiopathPointer == IntPtr.Zero)
            {
                return true;
            }

            DestroyAudiopathDelegate method = GetMethod<DestroyAudiopathDelegate>(MethodDestroyAudiopath);
            if (method == null)
            {
                return false;
            }

            byte result = method(_loaderBackend.DriverInstance, audiopathPointer);
            return result != 0;
        }

        public bool DestroySegment(object? segmentHandle)
        {
            IntPtr segmentPointer = UnwrapHandle(segmentHandle);
            if (segmentPointer == IntPtr.Zero)
            {
                return true;
            }

            DestroySegmentDelegate method = GetMethod<DestroySegmentDelegate>(MethodDestroySegment);
            if (method == null)
            {
                return false;
            }

            byte result = method(_loaderBackend.DriverInstance, segmentPointer);
            return result != 0;
        }

        public bool GetPlaybackStateOfSegment(object? segmentHandle, out byte state)
        {
            state = 0;
            IntPtr segmentPointer = UnwrapHandle(segmentHandle);
            if (segmentPointer == IntPtr.Zero)
            {
                return false;
            }

            GetPlaybackStateDelegate method = GetMethod<GetPlaybackStateDelegate>(MethodGetPlaybackStateOfSegment);
            if (method == null)
            {
                return false;
            }

            byte result = method(_loaderBackend.DriverInstance, segmentPointer, out state);
            return result != 0;
        }

        public bool LoadCachedObject(IDmObject obj, string? rootPath, out object? segmentHandle, out object? loadedState)
        {
            segmentHandle = null;
            loadedState = null;

            if (_loaderBackend.LoaderContext == IntPtr.Zero)
            {
                LastError = "DMManager: loader context missing or not reconstructed yet.";
                return false;
            }

            string objectPath = BuildObjectPath(obj, rootPath);
            if (string.IsNullOrWhiteSpace(objectPath))
            {
                LastError = "DMManager: unresolved segment path.";
                return false;
            }

            LoadCachedObjectDelegate method = GetMethod<LoadCachedObjectDelegate>(MethodLoadCachedObject);
            if (method == null)
            {
                return false;
            }

            IntPtr namePointer = Marshal.StringToHGlobalAnsi(objectPath);
            IntPtr statePointer = IntPtr.Zero;
            IntPtr segmentPointer = namePointer;

            try
            {
                byte result = method(_loaderBackend.DriverInstance, 0, ref statePointer, ref segmentPointer, _loaderBackend.LoaderContext);
                if (result == 0)
                {
                    return false;
                }

                segmentHandle = new NativeHandle(segmentPointer);
                loadedState = statePointer == IntPtr.Zero ? null : new NativeHandle(statePointer);
                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(namePointer);
            }
        }

        public bool ResetSegmentPlayback(object? segmentHandle, int value)
        {
            IntPtr segmentPointer = UnwrapHandle(segmentHandle);
            if (segmentPointer == IntPtr.Zero)
            {
                return false;
            }

            ResetSegmentPlaybackDelegate method = GetMethod<ResetSegmentPlaybackDelegate>(MethodResetSegmentPlayback);
            if (method == null)
            {
                return false;
            }

            byte result = method(_loaderBackend.DriverInstance, segmentPointer, value);
            return result != 0;
        }

        public bool SetVolumeOfAudiopath(object? audiopath, int volume, int rampMilliseconds)
        {
            IntPtr audiopathPointer = UnwrapHandle(audiopath);
            if (audiopathPointer == IntPtr.Zero)
            {
                return false;
            }

            SetVolumeDelegate method = GetMethod<SetVolumeDelegate>(MethodSetVolumeOfAudiopath);
            if (method == null)
            {
                return false;
            }

            byte result = method(_loaderBackend.DriverInstance, audiopathPointer, volume, rampMilliseconds);
            return result != 0;
        }

        public bool StartSegmentPlayback(object? audiopath, object? segmentHandle, int flags, int startTime, int repeatCount, int unknown)
        {
            IntPtr audiopathPointer = UnwrapHandle(audiopath);
            IntPtr segmentPointer = UnwrapHandle(segmentHandle);
            if (audiopathPointer == IntPtr.Zero || segmentPointer == IntPtr.Zero)
            {
                return false;
            }

            StartSegmentDelegate method = GetMethod<StartSegmentDelegate>(MethodStartSegmentPlayback);
            if (method == null)
            {
                return false;
            }

            byte result = method(_loaderBackend.DriverInstance, audiopathPointer, segmentPointer, flags, startTime, repeatCount, unknown);
            return result != 0;
        }

        public void ShutdownDriver()
        {
            _loaderBackend.Shutdown();
        }

        public void ShutdownLoader()
        {
            _loaderBackend.Shutdown();
        }

        public void ShutdownPerformance()
        {
            _loaderBackend.Shutdown();
        }

        private string BuildObjectPath(IDmObject obj, string? rootPath)
        {
            if (obj is DmObject concrete && !string.IsNullOrWhiteSpace(concrete.ResolvedPath))
            {
                return concrete.ResolvedPath;
            }

            if (!string.IsNullOrWhiteSpace(rootPath))
            {
                string? resolvedPath = FileDmObjectRepository.ResolveSegmentPath(rootPath, obj.SegmentName);
                if (!string.IsNullOrWhiteSpace(resolvedPath))
                {
                    return resolvedPath;
                }
            }

            return obj.SegmentName;
        }

        private T? GetMethod<T>(int offset) where T : class
        {
            IntPtr methodPointer = Marshal.ReadIntPtr(_loaderBackend.MethodTable, offset);
            if (methodPointer == IntPtr.Zero)
            {
                LastError = "DMManager: method table entry missing.";
                return null;
            }

            return Marshal.GetDelegateForFunctionPointer(methodPointer, typeof(T)) as T;
        }

        private static IntPtr UnwrapHandle(object? value)
        {
            return value switch
            {
                NativeHandle native => native.Pointer,
                IntPtr directPointer => directPointer,
                _ => IntPtr.Zero,
            };
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DmAudiopathConfig
        {
            internal int Mode;
            internal int Config;
            internal int Reserved08;
            internal int Reserved0C;
        }

        internal sealed class NativeHandle
        {
            internal NativeHandle(IntPtr pointer)
            {
                Pointer = pointer;
            }

            internal IntPtr Pointer { get; }
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate byte LoadCachedObjectDelegate(IntPtr driverInstance, int mode, ref IntPtr loadedState, ref IntPtr segmentHandle, IntPtr loaderContext);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate byte CreateAudiopathDelegate(IntPtr driverInstance, ref DmAudiopathConfig config, out IntPtr audiopath, IntPtr segmentHandle);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate byte ActivateAudiopathDelegate(IntPtr driverInstance, IntPtr audiopath, int active);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate byte DestroyAudiopathDelegate(IntPtr driverInstance, IntPtr audiopath);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate byte DestroySegmentDelegate(IntPtr driverInstance, IntPtr segmentHandle);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate byte SetVolumeDelegate(IntPtr driverInstance, IntPtr audiopath, int volume, int rampMilliseconds);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate byte StartSegmentDelegate(IntPtr driverInstance, IntPtr audiopath, IntPtr segmentHandle, int flags, int startTime, int repeatCount, int unknown);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate byte ResetSegmentPlaybackDelegate(IntPtr driverInstance, IntPtr segmentHandle, int value);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate byte GetPlaybackStateDelegate(IntPtr driverInstance, IntPtr segmentHandle, out byte state);
    }
}
