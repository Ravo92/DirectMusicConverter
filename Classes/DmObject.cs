using DirectMusicConverter.Interfaces;

namespace DirectMusicConverter.Classes
{
    internal sealed class DmObject : IDmObject
    {
        internal DmObject(int type, int variant, string segmentName, string? resolvedPath)
        {
            Type = type;
            Variant = variant;
            SegmentName = segmentName;
            ResolvedPath = resolvedPath;
        }

        public int Type { get; }

        public int Variant { get; }

        public string SegmentName { get; }

        internal string? ResolvedPath { get; }
    }
}
