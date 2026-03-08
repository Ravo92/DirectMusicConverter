namespace DirectMusicConverter.Classes
{
    internal sealed class DmSegmentSlot
    {
        internal bool IsUsed;
        internal byte Unknown01;
        internal byte Unknown02;
        internal byte Unknown03;
        internal object? SegmentHandle;  // Proprietary DLL segment wrapper.
        internal object? Field08;        // Native scratch / load record.
        internal object? Field0C;
        internal int Type;
        internal int Variant;
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
