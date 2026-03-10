using DirectMusicConverter.Classes;
using DirectMusicConverter.Interfaces;
using DirectMusicConverter.Logger.Enums;

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

            string logPath = Path.Combine(AppContext.BaseDirectory, "directmusic_debug.log");
            Logger.Logger.Initialize(logPath);
            Logger.Logger.MinimumLevel = LogLevel.Trace;

            Logger.Logger.Info("Program", "Startup");
            Logger.Logger.Info("Program", "Args: gameRoot='" + gameRoot + "', type=" + type + ", variant=" + variant + ", driverDirectory='" + driverDirectory + "', synthMode=" + synthMode + ", masterVolume=" + masterVolume);
            Logger.Logger.Info("Program", "Process bitness=" + (Environment.Is64BitProcess ? "x64" : "x86"));

            if (!Directory.Exists(gameRoot))
            {
                Logger.Logger.Error("Program", "Input directory does not exist: " + gameRoot);
                Console.WriteLine("Input directory does not exist: " + gameRoot);
                return 2;
            }

            string dmRootPath = Path.Combine(gameRoot, "data", "dm2");
            if (!Directory.Exists(dmRootPath))
            {
                Logger.Logger.Error("Program", "DirectMusic directory does not exist: " + dmRootPath);
                Console.WriteLine("DirectMusic directory does not exist: " + dmRootPath);
                return 2;
            }

            bool sampleRateDetected = AudioDeviceSampleRateDetector.TryGetDefaultRenderSampleRate(out int detectedSampleRate, out string? sampleRateDetectionError);
            if (sampleRateDetected)
            {
                Logger.Logger.Info("Program", "Detected default render sample rate=" + detectedSampleRate + " Hz.");
            }
            else
            {
                Logger.Logger.Warning("Program", "Default render sample rate detection failed. Falling back to synth-mode defaults. Error='" + (sampleRateDetectionError ?? "<null>") + "'");
                detectedSampleRate = 0;
            }

            using Gedx8MusicDriverLoaderBackend loaderBackend = new(driverDirectory, synthMode, detectedSampleRate);
            loaderBackend.SetSearchDirectory(dmRootPath);

            Console.WriteLine("Driver dir      : " + driverDirectory);
            Console.WriteLine("Synth mode      : " + synthMode);
            Console.WriteLine("Master volume   : " + masterVolume);
            Console.WriteLine("Sample rate     : " + (sampleRateDetected ? detectedSampleRate + " Hz (device default)" : "auto fallback (synth-mode defaults)"));
            Console.WriteLine("Log file        : " + logPath);

            Logger.Logger.Info("Program", "Creating loader backend.");
            bool ok = loaderBackend.CreatePerformance();
            ok &= loaderBackend.CreateComposer();
            ok &= loaderBackend.InitializeSynthesizer();
            ok &= loaderBackend.CreateLoaderContext();
            if (!ok)
            {
                Logger.Logger.Error("Program", "Loader initialization failed. LastError='" + (loaderBackend.LastError ?? "<null>") + "'");
                Console.WriteLine(loaderBackend.LastError ?? "Loader initialization failed.");
                return 3;
            }

            IDmObjectRepository repository = new FileDmObjectRepository(dmRootPath);
            Gedx8MusicDriverPlaybackBackend playbackBackend = new(loaderBackend);
            if (!playbackBackend.SetMasterVolume(masterVolume))
            {
                Logger.Logger.Error("Program", "SetMasterVolume failed. LastError='" + (playbackBackend.LastError ?? "<null>") + "'");
                Console.WriteLine(playbackBackend.LastError ?? "DMManager: geSetMasterVolume failed.");
                return 4;
            }

            DmManager manager = new(dmRootPath, synthMode, loaderBackend.AudioPathConfig);
            manager.MarkInitialized();

            string? segmentName = manager.ResolveSegmentName(type, variant);

            Console.WriteLine("Game root       : " + gameRoot);
            Console.WriteLine("DM root         : " + dmRootPath);
            Console.WriteLine("Type            : 0x" + type.ToString("X2") + " (" + type + ")");
            Console.WriteLine("Variant         : " + variant);
            Console.WriteLine("AudiopathConfig : 0x" + loaderBackend.AudioPathConfig.ToString("X2"));
            Console.WriteLine("Segment         : " + (segmentName ?? "<none>"));

            Logger.Logger.Info("Program", "Resolved segment: '" + (segmentName ?? "<none>") + "'");

            bool started = manager.StartOrSwitchSegment(type, variant, repository, playbackBackend, unchecked((uint)Environment.TickCount));
            if (!started)
            {
                Logger.Logger.Error("Program", "Playback start failed. ManagerError='" + (manager.LastError ?? "<null>") + "', PlaybackError='" + (playbackBackend.LastError ?? "<null>") + "'");
                Console.WriteLine(manager.LastError ?? playbackBackend.LastError ?? "Playback start failed.");
                return 5;
            }

            Logger.Logger.Info("Program", "Playback started successfully.");
            Console.WriteLine("Playback started. Press ENTER to stop and shut down.");
            Console.ReadLine();

            Logger.Logger.Info("Program", "Stopping playback.");
            manager.Shutdown(playbackBackend);
            Logger.Logger.Info("Program", "Shutdown complete.");
            return 0;
        }
    }
}
