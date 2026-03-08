using DirectMusicConverter.Classes;
using DirectMusicConverter.Interfaces;

namespace DirectMusicConverter
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string gameRoot = args.Length >= 1 ? args[0] : Environment.CurrentDirectory;
            int type = args.Length >= 2 && int.TryParse(args[1], out int parsedType) ? parsedType : 0x03;
            int variant = args.Length >= 3 && int.TryParse(args[2], out int parsedVariant) ? parsedVariant : 4;
            string driverDirectory = args.Length >= 4 ? args[3] : gameRoot;
            int synthMode = args.Length >= 5 && int.TryParse(args[4], out int parsedSynthMode) ? parsedSynthMode : 0;
            int masterVolume = args.Length >= 6 && int.TryParse(args[5], out int parsedMasterVolume) ? parsedMasterVolume : 100;

            if (!Directory.Exists(gameRoot))
            {
                Console.WriteLine("Input directory does not exist: " + gameRoot);
                return 2;
            }

            string dmRootPath = Path.Combine(gameRoot, "data", "dm2");
            if (!Directory.Exists(dmRootPath))
            {
                Console.WriteLine("DirectMusic directory does not exist: " + dmRootPath);
                return 2;
            }

            using Gedx8MusicDriverLoaderBackend loaderBackend = new(driverDirectory, synthMode);
            loaderBackend.SetSearchDirectory(dmRootPath);

            Console.WriteLine("Driver dir      : " + driverDirectory);
            Console.WriteLine("Synth mode      : " + synthMode);
            Console.WriteLine("Master volume   : " + masterVolume);

            bool ok = loaderBackend.CreatePerformance();
            ok &= loaderBackend.CreateComposer();
            ok &= loaderBackend.InitializeSynthesizer();
            ok &= loaderBackend.CreateLoaderContext();
            if (!ok)
            {
                Console.WriteLine(loaderBackend.LastError ?? "Loader initialization failed.");
                return 3;
            }

            IDmObjectRepository repository = new FileDmObjectRepository(dmRootPath);
            Gedx8MusicDriverPlaybackBackend playbackBackend = new(loaderBackend);
            if (!playbackBackend.SetMasterVolume(masterVolume))
            {
                Console.WriteLine(playbackBackend.LastError ?? "DMManager: geSetMasterVolume failed.");
                return 4;
            }

            DmManager manager = new(dmRootPath, synthMode, loaderBackend.AudioPathConfig);
            manager.MarkInitialized();

            Console.WriteLine("Game root       : " + gameRoot);
            Console.WriteLine("DM root         : " + dmRootPath);
            Console.WriteLine("Type            : 0x" + type.ToString("X2") + " (" + type + ")");
            Console.WriteLine("Variant         : " + variant);
            Console.WriteLine("AudiopathConfig : 0x" + loaderBackend.AudioPathConfig.ToString("X2"));
            Console.WriteLine("Segment         : " + (manager.ResolveSegmentName(type, variant) ?? "<none>"));

            bool started = manager.StartOrSwitchSegment(type, variant, repository, playbackBackend, unchecked((uint)Environment.TickCount));
            if (!started)
            {
                Console.WriteLine(manager.LastError ?? playbackBackend.LastError ?? "Playback start failed.");
                return 5;
            }

            Console.WriteLine("Playback started. Press ENTER to stop and shut down.");
            Console.ReadLine();

            manager.ResetAllSegmentPlaybackStates(playbackBackend);
            manager.Shutdown(playbackBackend);
            return 0;
        }
    }
}
