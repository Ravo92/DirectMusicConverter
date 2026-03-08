namespace DirectMusicConverter.Interfaces
{
    internal interface IDmLoaderBackend
    {
        bool InitializeSynthesizer();
        bool CreatePerformance();
        bool CreateComposer();
        bool CreateLoaderContext();
        void Shutdown();
    }
}
