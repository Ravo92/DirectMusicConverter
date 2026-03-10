using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace DirectMusicConverter.Classes
{
    [SupportedOSPlatform("windows")]
    internal static class AudioDeviceSampleRateDetector
    {
        private static readonly Guid ClsidMmDeviceEnumerator = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
        private static readonly Guid IidIAudioClient = new("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2");

        internal static bool TryGetDefaultRenderSampleRate(out int sampleRate, out string? error)
        {
            sampleRate = 0;
            error = null;

            IMMDeviceEnumerator? enumerator = null;
            IMMDevice? device = null;
            IAudioClient? audioClient = null;
            IntPtr mixFormatPointer = IntPtr.Zero;

            try
            {
                Type? enumeratorType = Type.GetTypeFromCLSID(ClsidMmDeviceEnumerator, throwOnError: false);
                if (enumeratorType == null)
                {
                    error = "MMDeviceEnumerator COM class unavailable.";
                    return false;
                }

                enumerator = Activator.CreateInstance(enumeratorType) as IMMDeviceEnumerator;
                if (enumerator == null)
                {
                    error = "Failed to create MMDeviceEnumerator COM object.";
                    return false;
                }

                int hr = enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out device);
                Marshal.ThrowExceptionForHR(hr);

                Guid iidIAudioClient = IidIAudioClient;
                hr = device.Activate(ref iidIAudioClient, CLSCTX.CLSCTX_ALL, IntPtr.Zero, out object? audioClientUnknown);
                Marshal.ThrowExceptionForHR(hr);

                audioClient = audioClientUnknown as IAudioClient;
                if (audioClient == null)
                {
                    error = "Failed to acquire IAudioClient.";
                    return false;
                }

                hr = audioClient.GetMixFormat(out mixFormatPointer);
                Marshal.ThrowExceptionForHR(hr);

                if (mixFormatPointer == IntPtr.Zero)
                {
                    error = "IAudioClient::GetMixFormat returned null.";
                    return false;
                }

                WaveFormatEx mixFormat = Marshal.PtrToStructure<WaveFormatEx>(mixFormatPointer);
                if (mixFormat.nSamplesPerSec <= 0)
                {
                    error = "Detected mix format has invalid sample rate.";
                    return false;
                }

                sampleRate = checked((int)mixFormat.nSamplesPerSec);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                if (mixFormatPointer != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(mixFormatPointer);
                }

                if (audioClient != null)
                {
                    Marshal.ReleaseComObject(audioClient);
                }

                if (device != null)
                {
                    Marshal.ReleaseComObject(device);
                }

                if (enumerator != null)
                {
                    Marshal.ReleaseComObject(enumerator);
                }
            }
        }

        [Flags]
        private enum CLSCTX : uint
        {
            CLSCTX_INPROC_SERVER = 0x1,
            CLSCTX_INPROC_HANDLER = 0x2,
            CLSCTX_LOCAL_SERVER = 0x4,
            CLSCTX_REMOTE_SERVER = 0x10,
            CLSCTX_ALL = CLSCTX_INPROC_SERVER | CLSCTX_INPROC_HANDLER | CLSCTX_LOCAL_SERVER | CLSCTX_REMOTE_SERVER,
        }

        private enum EDataFlow
        {
            eRender = 0,
            eCapture = 1,
            eAll = 2,
        }

        private enum ERole
        {
            eConsole = 0,
            eMultimedia = 1,
            eCommunications = 2,
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WaveFormatEx
        {
            internal ushort wFormatTag;
            internal ushort nChannels;
            internal uint nSamplesPerSec;
            internal uint nAvgBytesPerSec;
            internal ushort nBlockAlign;
            internal ushort wBitsPerSample;
            internal ushort cbSize;
        }

        [ComImport]
        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            [PreserveSig]
            int EnumAudioEndpoints(EDataFlow dataFlow, uint stateMask, out IntPtr devices);

            [PreserveSig]
            int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice endpoint);

            [PreserveSig]
            int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);

            [PreserveSig]
            int RegisterEndpointNotificationCallback(IntPtr client);

            [PreserveSig]
            int UnregisterEndpointNotificationCallback(IntPtr client);
        }

        [ComImport]
        [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig]
            int Activate(ref Guid iid, CLSCTX clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.Interface)] out object interfacePointer);

            [PreserveSig]
            int OpenPropertyStore(int stgmAccess, out IntPtr properties);

            [PreserveSig]
            int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);

            [PreserveSig]
            int GetState(out uint state);
        }

        [ComImport]
        [Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioClient
        {
            [PreserveSig]
            int Initialize(int shareMode, int streamFlags, long bufferDuration, long periodicity, IntPtr format, IntPtr audioSessionGuid);

            [PreserveSig]
            int GetBufferSize(out uint bufferFrameCount);

            [PreserveSig]
            int GetStreamLatency(out long latency);

            [PreserveSig]
            int GetCurrentPadding(out uint paddingFrameCount);

            [PreserveSig]
            int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closestMatch);

            [PreserveSig]
            int GetMixFormat(out IntPtr deviceFormat);

            [PreserveSig]
            int GetDevicePeriod(out long defaultDevicePeriod, out long minimumDevicePeriod);

            [PreserveSig]
            int Start();

            [PreserveSig]
            int Stop();

            [PreserveSig]
            int Reset();

            [PreserveSig]
            int SetEventHandle(IntPtr eventHandle);

            [PreserveSig]
            int GetService(ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object service);
        }
    }
}