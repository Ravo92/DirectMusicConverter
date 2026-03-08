namespace DirectMusicConverter.Interfaces
{
    internal interface IDmObject
    {
        int Type { get; }
        int Variant { get; }
        string SegmentName { get; }
    }
}
