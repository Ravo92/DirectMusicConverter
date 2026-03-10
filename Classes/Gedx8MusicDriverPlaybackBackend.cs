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

            Logger.Logger.Info("PlaybackBackend", "LoadCachedObject entered. Type=0x" + obj.Type.ToString("X2") + ", Variant=" + obj.Variant + ", SegmentName='" + obj.SegmentName + "', RootPath='" + (rootPath ?? "<null>") + "'");

            string fullPath = BuildObjectPath(obj, rootPath);
            Logger.Logger.Info("PlaybackBackend", "Resolved fullPath='" + fullPath + "'");

            if (string.IsNullOrWhiteSpace(fullPath))
            {
                LastError = "DMManager: unresolved segment path.";
                Logger.Logger.Error("PlaybackBackend", LastError);
                return false;
            }

            string? fileName = Path.GetFileName(fullPath);
            string? baseDirectory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(baseDirectory))
            {
                LastError = "DMManager: segment path is incomplete.";
                Logger.Logger.Error("PlaybackBackend", LastError + " fullPath='" + fullPath + "'");
                return false;
            }

            string basePath = EnsureTrailingSlash(baseDirectory);
            Logger.Logger.Info("PlaybackBackend", "fileName='" + fileName + "', basePath='" + basePath + "'");

            IntPtr fileNamePointer = Marshal.StringToHGlobalAnsi(fileName);
            IntPtr basePathPointer = Marshal.StringToHGlobalAnsi(basePath);
            IntPtr nativeRecordPointer = Marshal.AllocHGlobal(NativeSegmentRecordSize);

            Logger.Logger.Info("PlaybackBackend", "Allocated native buffers: fileNamePointer=" + Logger.Logger.FormatPointer(fileNamePointer) + ", basePathPointer=" + Logger.Logger.FormatPointer(basePathPointer) + ", nativeRecordPointer=" + Logger.Logger.FormatPointer(nativeRecordPointer));

            try
            {
                ZeroMemory(nativeRecordPointer, NativeSegmentRecordSize);

                Marshal.WriteIntPtr(nativeRecordPointer, 0x08, fileNamePointer);
                Marshal.WriteInt32(nativeRecordPointer, 0x0C, obj.Type);
                Marshal.WriteInt32(nativeRecordPointer, 0x10, obj.Variant);

                Logger.Logger.DumpBytes("PlaybackBackend", "Native record before geLoadCachedObject", nativeRecordPointer, NativeSegmentRecordSize);

                LoadCachedObjectDelegate? method = GetMethod<LoadCachedObjectDelegate>(MethodLoadCachedObject);
                if (method == null)
                {
                    Logger.Logger.Error("PlaybackBackend", "LoadCachedObject method table lookup failed. LastError='" + (LastError ?? "<null>") + "'");
                    return false;
                }

                IntPtr wrapperOutputPointer = IntPtr.Add(nativeRecordPointer, 0x04);
                Logger.Logger.Info("PlaybackBackend", "Calling geLoadCachedObject with DriverInstance=" + Logger.Logger.FormatPointer(_loaderBackend.DriverInstance) + ", mode=0, wrapperOutputPointer=" + Logger.Logger.FormatPointer(wrapperOutputPointer) + ", nativeRecordPointer=" + Logger.Logger.FormatPointer(nativeRecordPointer) + ", basePathPointer=" + Logger.Logger.FormatPointer(basePathPointer));

                byte result = method(_loaderBackend.DriverInstance, 0, wrapperOutputPointer, nativeRecordPointer, basePathPointer);

                Logger.Logger.Info("PlaybackBackend", "geLoadCachedObject result=" + result);
                Logger.Logger.DumpBytes("PlaybackBackend", "Native record after geLoadCachedObject", nativeRecordPointer, NativeSegmentRecordSize);

                if (result == 0)
                {
                    LastError = "DMManager: geLoadCachedObject failed.";
                    Logger.Logger.Error("PlaybackBackend", LastError);
                    return false;
                }

                IntPtr wrapperPointer = Marshal.ReadIntPtr(nativeRecordPointer, 0x04);
                Logger.Logger.Info("PlaybackBackend", "WrapperPointer=" + Logger.Logger.FormatPointer(wrapperPointer));

                if (wrapperPointer == IntPtr.Zero)
                {
                    LastError = "DMManager: geLoadCachedObject returned null segment wrapper.";
                    Logger.Logger.Error("PlaybackBackend", LastError);
                    return false;
                }

                NativeSegmentHandle nativeSegmentHandle = new(nativeRecordPointer, fileNamePointer, basePathPointer);
                segmentHandle = nativeSegmentHandle;
                loadedState = new NativeHandle(nativeRecordPointer);

                Logger.Logger.Info("PlaybackBackend", "LoadCachedObject succeeded. SegmentHandle wrapper=" + Logger.Logger.FormatPointer(nativeSegmentHandle.WrapperPointer));

                nativeRecordPointer = IntPtr.Zero;
                fileNamePointer = IntPtr.Zero;
                basePathPointer = IntPtr.Zero;
                return true;
            }
            catch (Exception ex)
            {
                LastError = "DMManager: LoadCachedObject threw exception. " + ex.Message;
                Logger.Logger.Error("PlaybackBackend", LastError, ex);
                return false;
            }
            finally
            {
                if (nativeRecordPointer != IntPtr.Zero)
                {
                    Logger.Logger.Debug("PlaybackBackend", "Freeing nativeRecordPointer=" + Logger.Logger.FormatPointer(nativeRecordPointer));
                    Marshal.FreeHGlobal(nativeRecordPointer);
                }

                if (fileNamePointer != IntPtr.Zero)
                {
                    Logger.Logger.Debug("PlaybackBackend", "Freeing fileNamePointer=" + Logger.Logger.FormatPointer(fileNamePointer));
                    Marshal.FreeHGlobal(fileNamePointer);
                }

                if (basePathPointer != IntPtr.Zero)
                {
                    Logger.Logger.Debug("PlaybackBackend", "Freeing basePathPointer=" + Logger.Logger.FormatPointer(basePathPointer));
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
            if (_loaderBackend.MethodTable == IntPtr.Zero)
            {
                LastError = "DMManager: loader method table is null.";
                Logger.Logger.Error("PlaybackBackend", LastError);
                return null;
            }

            IntPtr methodPointer = Marshal.ReadIntPtr(_loaderBackend.MethodTable, offset);
            Logger.Logger.Debug("PlaybackBackend", "Method lookup offset=0x" + offset.ToString("X2") + " => " + Logger.Logger.FormatPointer(methodPointer));

            if (methodPointer == IntPtr.Zero)
            {
                LastError = "DMManager: method table entry missing at 0x" + offset.ToString("X2") + ".";
                Logger.Logger.Error("PlaybackBackend", LastError);
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
