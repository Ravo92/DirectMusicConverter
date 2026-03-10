namespace DirectMusicConverter.Interfaces
{
    internal interface IDmPlaybackBackend
    {
        bool LoadCachedObject(IDmObject obj, string? rootPath, out object? segmentHandle, out object? loadedState);
        bool SetMasterVolume(int volumePercent);
        bool CreateAudiopath(int audiopathMode, int config, object? segmentHandle, out object? audiopath);
        bool ActivateAudiopath(object? audiopath, bool active);
        bool DestroyAudiopath(object? audiopath);
        bool DestroySegment(object? segmentHandle);
        bool SetVolumeOfAudiopath(object? audiopath, int volume, int rampMilliseconds);
        bool StartSegmentPlayback(object? audiopath, object? segmentHandle, int flags, int startTime, int repeatCount, int unknown);
        bool ResetSegmentPlayback(object? segmentHandle, int value);
        bool GetPlaybackStateOfSegment(object? segmentHandle, out byte state);
        void ShutdownPerformance();
        void ShutdownLoader();
        void ShutdownDriver();
    }
}