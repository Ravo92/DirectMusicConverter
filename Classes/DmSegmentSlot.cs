namespace DirectMusicConverter.Classes
{
    internal sealed class DmSegmentSlot
    {
        internal bool IsUsed;            // +0x00
        internal byte Unknown01;         // +0x01
        internal byte Unknown02;         // +0x02
        internal byte Unknown03;         // +0x03
        internal object? SegmentHandle;  // +0x04
        internal object? Field08;        // +0x08
        internal object? Field0C;        // +0x0C
        internal int Type;               // +0x10
        internal int Variant;            // +0x14
        internal string? SegmentName;
        internal Interfaces.IDmObject? LoadedObject;

        internal void Clear()
        {
            IsUsed = false;
            Unknown01 = 0;
            Unknown02 = 0;
            Unknown03 = 0;
            SegmentHandle = null;
            Field08 = null;
            Field0C = null;
            Type = 0;
            Variant = 0;
            SegmentName = null;
            LoadedObject = null;
        }
    }
}
