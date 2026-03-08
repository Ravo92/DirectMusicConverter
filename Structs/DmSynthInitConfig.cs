using System.Runtime.InteropServices;

namespace DirectMusicConverter.Structs
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct DmSynthInitConfig
    {
        internal int Reserved00;
        internal int SampleRate;
        internal int Config;
    }
}
