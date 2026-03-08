namespace DirectMusicConverter.Interfaces
{
    internal interface IDmObjectRepository
    {
        IDmObject? LoadObject(int type, int variant, string segmentName);
    }
}
