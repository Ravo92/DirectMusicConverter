using DirectMusicConverter.Classes;
using DirectMusicConverter.Interfaces;

namespace DirectMusicConverter
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string rootPath = args.Length >= 1 ? args[0] : Environment.CurrentDirectory;
            int type = args.Length >= 2 && int.TryParse(args[1], out int parsedType) ? parsedType : 0x03;
            int variant = args.Length >= 3 && int.TryParse(args[2], out int parsedVariant) ? parsedVariant : 4;
            string? driverDirectory = args.Length >= 4 ? args[3] : null;
            int initMode = args.Length >= 5 && int.TryParse(args[4], out int parsedInitMode) ? parsedInitMode : 0;

            if (!Directory.Exists(rootPath))
            {
                Console.WriteLine("Input directory does not exist: " + rootPath);
                return 2;
            }

            using Gedx8MusicDriverLoaderBackend loaderBackend = new(driverDirectory, initMode);

            Console.WriteLine("Driver dir : " + (driverDirectory ?? "<null>"));
            Console.WriteLine("Init mode  : " + initMode);

            bool ok = loaderBackend.CreatePerformance();
            ok &= loaderBackend.CreateComposer();
            ok &= loaderBackend.InitializeSynthesizer();
            if (!ok)
            {
                Console.WriteLine(loaderBackend.LastError ?? "Loader initialization failed.");
                Console.WriteLine("Press ENTER to exit.");
                Console.ReadLine();
                return 3;
            }

            Console.WriteLine("SampleRate : " + loaderBackend.SampleRate);
            Console.WriteLine("AP Config  : 0x" + loaderBackend.AudiopathConfig.ToString("X"));

            ok = loaderBackend.CreateLoaderContext();
            if (!ok)
            {
                Console.WriteLine(loaderBackend.LastError ?? "Loader context creation failed.");
                Console.WriteLine("Press ENTER to exit.");
                Console.ReadLine();
                return 3;
            }

            IDmObjectRepository repository = new FileDmObjectRepository(rootPath);
            IDmPlaybackBackend playbackBackend = new Gedx8MusicDriverPlaybackBackend(loaderBackend);
            DmManager manager = new(rootPath, 0, loaderBackend.AudiopathConfig);
            manager.MarkInitialized();

            Console.WriteLine("Root    : " + rootPath);
            Console.WriteLine("Type    : 0x" + type.ToString("X2") + " (" + type + ")");
            Console.WriteLine("Variant : " + variant);
            Console.WriteLine("Segment : " + (manager.ResolveSegmentName(type, variant) ?? "<none>"));

            bool started = manager.StartOrSwitchSegment(type, variant, repository, playbackBackend, unchecked((uint)Environment.TickCount));
            if (!started)
            {
                Console.WriteLine(manager.LastError ?? "Playback start failed.");
                return 4;
            }

            Console.WriteLine("Playback started. Press ENTER to stop and shut down.");
            Console.ReadLine();

            manager.ResetAllSegmentPlaybackStates(playbackBackend);
            manager.Shutdown(playbackBackend);
            return 0;
        }
    }
}
