using System.Runtime.InteropServices;
using DirectMusicConverter.Interfaces;

namespace DirectMusicConverter.Classes
{
    internal sealed class Gedx8MusicDriverPlaybackBackend : IDmPlaybackBackend
    {
        private const int MethodSetMasterVolume = 0x18;
        private const int MethodLoadCachedObject = 0x20;
        private const int MethodCreateAudiopath = 0x34;
        private const int MethodActivateAudiopath = 0x38;
        private const int MethodSetVolumeOfAudiopath = 0x3C;
        private const int MethodDestroyAudiopath = 0x50;
        private const int MethodStartSegmentPlayback = 0x54;
        private const int MethodResetSegmentPlayback = 0x58;
        private const int MethodGetPlaybackStateOfSegment = 0x5C;
        private const int MethodDestroySegment = 0x6C;
        private const int NativeSegmentRecordSize = 0x18;

        private readonly Gedx8MusicDriverLoaderBackend _loaderBackend;

        internal Gedx8MusicDriverPlaybackBackend(Gedx8MusicDriverLoaderBackend loaderBackend)
        {
            _loaderBackend = loaderBackend;
        }

        internal string? LastError { get; private set; }

        public bool LoadCachedObject(IDmObject obj, string? rootPath, out object? segmentHandle, out object? loadedState)
        {
            segmentHandle = null;
            loadedState = null;

            string fullPath = BuildObjectPath(obj, rootPath);
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                LastError = "DMManager: unresolved segment path.";
                return false;
            }

            string? fileName = Path.GetFileName(fullPath);
            string? baseDirectory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(baseDirectory))
            {
                LastError = "DMManager: segment path is incomplete.";
                return false;
            }

            string basePath = EnsureTrailingSlash(baseDirectory);
            IntPtr fileNamePointer = Marshal.StringToHGlobalAnsi(fileName);
            IntPtr basePathPointer = Marshal.StringToHGlobalAnsi(basePath);
            IntPtr nativeRecordPointer = Marshal.AllocHGlobal(NativeSegmentRecordSize);

            try
            {
                ZeroMemory(nativeRecordPointer, NativeSegmentRecordSize);

                // Mirrors the in-game temporary record at DMManager slot+0x40:
                // +0x00 = unknown / mode-specific field (zeroed before load)
                // +0x04 = out wrapper pointer written by geLoadCachedObject
                // +0x08 = ANSI segment file name pointer
                // +0x0C = type
                // +0x10 = variant
                // +0x14 = trailing scratch / zero
                Marshal.WriteIntPtr(nativeRecordPointer, 0x08, fileNamePointer);
                Marshal.WriteInt32(nativeRecordPointer, 0x0C, obj.Type);
                Marshal.WriteInt32(nativeRecordPointer, 0x10, obj.Variant);

                LoadCachedObjectDelegate? method = GetMethod<LoadCachedObjectDelegate>(MethodLoadCachedObject);
                if (method == null)
                {
                    return false;
                }

                IntPtr wrapperOutputPointer = IntPtr.Add(nativeRecordPointer, 0x04);
                byte result = method(_loaderBackend.DriverInstance, 0, wrapperOutputPointer, nativeRecordPointer, basePathPointer);
                if (result == 0)
                {
                    LastError = "DMManager: geLoadCachedObject failed.";
                    return false;
                }

                IntPtr wrapperPointer = Marshal.ReadIntPtr(nativeRecordPointer, 0x04);
                if (wrapperPointer == IntPtr.Zero)
                {
                    LastError = "DMManager: geLoadCachedObject returned null segment wrapper.";
                    return false;
                }

                NativeSegmentHandle nativeSegmentHandle = new(nativeRecordPointer, fileNamePointer, basePathPointer);
                segmentHandle = nativeSegmentHandle;
                loadedState = new NativeHandle(nativeRecordPointer);

                nativeRecordPointer = IntPtr.Zero;
                fileNamePointer = IntPtr.Zero;
                basePathPointer = IntPtr.Zero;
                return true;
            }
            finally
            {
                if (nativeRecordPointer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(nativeRecordPointer);
                }

                if (fileNamePointer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(fileNamePointer);
                }

                if (basePathPointer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(basePathPointer);
                }
            }
        }

        public bool SetMasterVolume(int volumePercent)
        {
            SetMasterVolumeDelegate? method = GetMethod<SetMasterVolumeDelegate>(MethodSetMasterVolume);
            if (method == null)
            {
                return false;
            }

            int clampedPercent = Math.Clamp(volumePercent, 0, 100);
            int nativeVolume = ConvertMasterVolumePercentToNative(clampedPercent);
            byte result = method(_loaderBackend.DriverInstance, nativeVolume);
            if (result == 0)
            {
                LastError = "DMManager: geSetMasterVolume failed.";
                return false;
            }

            return true;
        }

        public bool ActivateAudiopath(object? audiopath, bool active)
        {
            IntPtr audiopathPointer = ResolveNativePointer(audiopath);
            if (audiopathPointer == IntPtr.Zero)
            {
                LastError = "DMManager: null audiopath wrapper.";
                return false;
            }

            ActivateAudiopathDelegate? method = GetMethod<ActivateAudiopathDelegate>(MethodActivateAudiopath);
            if (method == null)
            {
                return false;
            }

            byte result = method(_loaderBackend.DriverInstance, audiopathPointer, active ? 1 : 0);
            if (result == 0)
            {
                LastError = "DMManager: geActivateAudiopath failed.";
                return false;
            }

            return true;
        }

        public bool CreateAudiopath(int audiopathMode, int config, object? segmentHandle, out object? audiopath)
        {
            audiopath = null;

            CreateAudiopathDelegate? method = GetMethod<CreateAudiopathDelegate>(MethodCreateAudiopath);
            if (method == null)
            {
                return false;
            }

            DmAudiopathConfig nativeConfig = new DmAudiopathConfig
            {
                Mode = audiopathMode,
                Config = config,
            };

            IntPtr outAudiopath = IntPtr.Zero;
            IntPtr optionalSegmentWrapper = ResolveNativePointer(segmentHandle);
            byte result = method(_loaderBackend.DriverInstance, ref nativeConfig, ref outAudiopath, optionalSegmentWrapper);
            if (result == 0 || outAudiopath == IntPtr.Zero)
            {
                LastError = "DMManager: geCreateAudiopath failed.";
                return false;
            }

            audiopath = new NativeHandle(outAudiopath);
            return true;
        }

        public bool DestroyAudiopath(object? audiopath)
        {
            IntPtr audiopathPointer = ResolveNativePointer(audiopath);
            if (audiopathPointer == IntPtr.Zero)
            {
                return true;
            }

            DestroyAudiopathDelegate? method = GetMethod<DestroyAudiopathDelegate>(MethodDestroyAudiopath);
            if (method == null)
            {
                return false;
            }

            byte result = method(_loaderBackend.DriverInstance, audiopathPointer);
            if (result == 0)
            {
                LastError = "DMManager: geDestroyAudiopath failed.";
                return false;
            }

            return true;
        }

        public bool DestroySegment(object? segmentHandle)
        {
            IntPtr segmentPointer = ResolveNativePointer(segmentHandle);
            if (segmentPointer == IntPtr.Zero)
            {
                return true;
            }

            DestroySegmentDelegate? method = GetMethod<DestroySegmentDelegate>(MethodDestroySegment);
            if (method == null)
            {
                return false;
            }

            byte result = method(_loaderBackend.DriverInstance, segmentPointer);
            if (segmentHandle is NativeSegmentHandle nativeSegmentHandle)
            {
                nativeSegmentHandle.Dispose();
            }

            if (result == 0)
            {
                LastError = "DMManager: geDestroySegment failed.";
                return false;
            }

            return true;
        }

        public bool GetPlaybackStateOfSegment(object? segmentHandle, out byte state)
        {
            state = 0;
            IntPtr segmentPointer = ResolveNativePointer(segmentHandle);
            if (segmentPointer == IntPtr.Zero)
            {
                LastError = "DMManager: null segment wrapper.";
                return false;
            }

            GetPlaybackStateDelegate? method = GetMethod<GetPlaybackStateDelegate>(MethodGetPlaybackStateOfSegment);
            if (method == null)
            {
                return false;
            }

            byte result = method(_loaderBackend.DriverInstance, segmentPointer, out state);
            if (result == 0)
            {
                LastError = "DMManager: geGetPlaybackStateOfSegment failed.";
                return false;
            }

            return true;
        }

        public bool ResetSegmentPlayback(object? segmentHandle, int value)
        {
            IntPtr segmentPointer = ResolveNativePointer(segmentHandle);
            if (segmentPointer == IntPtr.Zero)
            {
                LastError = "DMManager: null segment wrapper.";
                return false;
            }

            ResetSegmentPlaybackDelegate? method = GetMethod<ResetSegmentPlaybackDelegate>(MethodResetSegmentPlayback);
            if (method == null)
            {
                return false;
            }

            byte result = method(_loaderBackend.DriverInstance, segmentPointer, value);
            if (result == 0)
            {
                LastError = "DMManager: geResetSegmentPlayback failed.";
                return false;
            }

            return true;
        }

        public bool SetVolumeOfAudiopath(object? audiopath, int volume, int rampMilliseconds)
        {
            IntPtr audiopathPointer = ResolveNativePointer(audiopath);
            if (audiopathPointer == IntPtr.Zero)
            {
                LastError = "DMManager: null audiopath wrapper.";
                return false;
            }

            SetVolumeDelegate? method = GetMethod<SetVolumeDelegate>(MethodSetVolumeOfAudiopath);
            if (method == null)
            {
                return false;
            }

            byte result = method(_loaderBackend.DriverInstance, audiopathPointer, volume, rampMilliseconds);
            if (result == 0)
            {
                LastError = "DMManager: geSetVolumeOfAudiopath failed.";
                return false;
            }

            return true;
        }

        public bool StartSegmentPlayback(object? audiopath, object? segmentHandle, int flags, int startTime, int repeatCount, int unknown)
        {
            IntPtr audiopathPointer = ResolveNativePointer(audiopath);
            IntPtr segmentPointer = ResolveNativePointer(segmentHandle);
            if (audiopathPointer == IntPtr.Zero || segmentPointer == IntPtr.Zero)
            {
                LastError = "DMManager: null audiopath or segment wrapper.";
                return false;
            }

            StartSegmentDelegate? method = GetMethod<StartSegmentDelegate>(MethodStartSegmentPlayback);
            if (method == null)
            {
                return false;
            }

            byte result = method(_loaderBackend.DriverInstance, audiopathPointer, segmentPointer, flags, startTime, repeatCount, unknown);
            if (result == 0)
            {
                LastError = "DMManager: geStartSegmentPlayback failed.";
                return false;
            }

            return true;
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
                LastError = "DMManager: method table entry missing at 0x" + offset.ToString("X2") + ".";
                return null;
            }

            return Marshal.GetDelegateForFunctionPointer(methodPointer, typeof(T)) as T;
        }

        private static int ConvertMasterVolumePercentToNative(int volumePercent)
        {
            if (volumePercent <= 0)
            {
                return unchecked((int)0xFFFFD8F0);
            }

            double scaled = volumePercent * 0.01;
            double db = Math.Log10(scaled) * 2000.0;
            int rounded = (int)Math.Round(db, MidpointRounding.AwayFromZero);
            return unchecked((int)0xFFFFFE0C) - rounded;
        }

        private static string EnsureTrailingSlash(string path)
        {
            if (path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar))
            {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }

        private static void ZeroMemory(IntPtr buffer, int size)
        {
            for (int i = 0; i < size; i++)
            {
                Marshal.WriteByte(buffer, i, 0);
            }
        }

        private static IntPtr ResolveNativePointer(object? value)
        {
            return value switch
            {
                NativeSegmentHandle segment => segment.WrapperPointer,
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
        }

        internal sealed class NativeHandle
        {
            internal NativeHandle(IntPtr pointer)
            {
                Pointer = pointer;
            }

            internal IntPtr Pointer { get; }
        }

        internal sealed class NativeSegmentHandle : IDisposable
        {
            internal NativeSegmentHandle(IntPtr nativeRecordPointer, IntPtr fileNamePointer, IntPtr basePathPointer)
            {
                NativeRecordPointer = nativeRecordPointer;
                FileNamePointer = fileNamePointer;
                BasePathPointer = basePathPointer;
            }

            internal IntPtr NativeRecordPointer { get; private set; }

            internal IntPtr WrapperPointer
            {
                get
                {
                    if (NativeRecordPointer == IntPtr.Zero)
                    {
                        return IntPtr.Zero;
                    }

                    return Marshal.ReadIntPtr(NativeRecordPointer, 0x04);
                }
            }

            internal IntPtr FileNamePointer { get; private set; }

            internal IntPtr BasePathPointer { get; private set; }

            public void Dispose()
            {
                if (NativeRecordPointer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(NativeRecordPointer);
                    NativeRecordPointer = IntPtr.Zero;
                }

                if (FileNamePointer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(FileNamePointer);
                    FileNamePointer = IntPtr.Zero;
                }

                if (BasePathPointer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(BasePathPointer);
                    BasePathPointer = IntPtr.Zero;
                }
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate byte SetMasterVolumeDelegate(IntPtr driverInstance, int nativeVolume);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate byte LoadCachedObjectDelegate(IntPtr driverInstance, int mode, IntPtr wrapperOutputPointer, IntPtr nativeRecordPointer, IntPtr basePathAnsi);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate byte CreateAudiopathDelegate(IntPtr driverInstance, ref DmAudiopathConfig config, ref IntPtr audiopath, IntPtr optionalSegmentWrapper);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate byte ActivateAudiopathDelegate(IntPtr driverInstance, IntPtr audiopath, int active);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate byte DestroyAudiopathDelegate(IntPtr driverInstance, IntPtr audiopath);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate byte DestroySegmentDelegate(IntPtr driverInstance, IntPtr segmentWrapper);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate byte SetVolumeDelegate(IntPtr driverInstance, IntPtr audiopath, int volume, int rampMilliseconds);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate byte StartSegmentDelegate(IntPtr driverInstance, IntPtr audiopath, IntPtr segmentWrapper, int flags, int startTime, int repeatCount, int unknown);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate byte ResetSegmentPlaybackDelegate(IntPtr driverInstance, IntPtr segmentWrapper, int value);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate byte GetPlaybackStateDelegate(IntPtr driverInstance, IntPtr segmentWrapper, out byte state);
    }
}
