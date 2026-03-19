using DirectMusicConverter.Interfaces;

namespace DirectMusicConverter.Classes
{
    internal sealed class DmManager
    {
        private const int FirstPlayableType = 0x02;
        private const int LastPlayableType = 0x26;
        private const int FirstSpecialType = 0x16;
        private const int LastSpecialType = 0x1E;
        private const int SegmentSlotCount = 20;
        private const int StartFlags = 0x2000;
        private const int StartTime = 0;
        private const int RepeatCount = -1;
        private const int StartUnknown = 1;
        private const int FadeDownVolume = unchecked((int)0xFFFFF830);
        private const int FadeDownMilliseconds = 300;

        private const int PlaybackStatePollCount = 40;
        private const int PlaybackStatePollSleepMilliseconds = 20;

        private readonly DmSegmentSlot[] _slots;
        private string? _rootPath;
        private int _currentType;
        private int _currentVariant;
        private uint _specialDeadline;
        private bool _specialVolumeApplied;
        private bool _isInitialized;
        private int _musicMode;
        private int _audiopathConfig;
        private object? _audiopath;
        private string? _lastError;

        internal DmManager(string? rootPath = null, int musicMode = 0, int audiopathConfig = 0x40)
        {
            _slots = new DmSegmentSlot[SegmentSlotCount];
            _rootPath = rootPath;
            _musicMode = musicMode;
            _audiopathConfig = audiopathConfig;

            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i] = new DmSegmentSlot();
            }
        }

        internal string? LastError => _lastError;

        internal void MarkInitialized()
        {
            _isInitialized = true;
        }

        internal static string? ResolveSegmentName(int type, int variant)
        {
            if (type < FirstPlayableType || type > LastPlayableType)
            {
                return null;
            }

            return type switch
            {
                0x03 => ResolveVariant(variant, "Theme_Viking_Friendly.sgt", "Theme_Viking_Neutral.sgt", "Theme_Viking_Hostile.sgt"), // 3
                0x04 => ResolveVariant(variant, "Theme_Franken_Friendly.sgt", "Theme_Franken_Neutral.sgt", "Theme_Franken_Hostile.sgt"), // 4
                0x06 => "Attack_Viking.sgt", // 6
                0x07 => "Attack_Franken.sgt", // 7
                0x08 => "Attack_Byzanz.sgt", // 8
                0x09 => "Attack_Arabs.sgt", // 9
                0x0A => ResolveVariant(variant, "Mission_Viking1_Standard.sgt", "Mission_Viking1_Wealthy.sgt", "Mission_Viking1_Danger.sgt"), // 10
                0x0B => ResolveVariant(variant, "Mission_Franken1_Standard.sgt", "Mission_Franken1_Wealthy.sgt", "Mission_Franken1_Danger.sgt"), // 11
                0x0C => ResolveVariant(variant, "Mission_Franken2_Standard.sgt", "Mission_Franken2_Wealthy.sgt", "Mission_Franken2_Danger.sgt"), // 12
                0x0D => ResolveVariant(variant, "Mission_Byzanz1_Standard.sgt", "Mission_Byzanz1_Wealthy.sgt", "Mission_Byzanz1_Danger.sgt"), // 13
                0x0E => ResolveVariant(variant, "Mission_Byzanz2_Standard.sgt", "Mission_Byzanz2_Wealthy.sgt", "Mission_Byzanz2_Danger.sgt"), // 14
                0x0F => ResolveVariant(variant, "Mission_Byzanz3_Standard.sgt", "Mission_Byzanz3_Wealthy.sgt", "Mission_Byzanz3_Danger.sgt"), // 15
                0x10 => ResolveVariant(variant, "Mission_Byzanz4_Standard.sgt", "Mission_Byzanz4_Wealthy.sgt", "Mission_Byzanz4_Danger.sgt"), // 16
                0x11 => ResolveVariant(variant, "Mission_Arabs1_Standard.sgt", "Mission_Arabs1_Wealthy.sgt", "Mission_Arabs1_Danger.sgt"), // 17
                0x12 => ResolveVariant(variant, "Mission_Arabs2_Standard.sgt", "Mission_Arabs2_Wealthy.sgt", "Mission_Arabs2_Danger.sgt"), // 18
                0x13 => ResolveVariant(variant, "Mission_Arabs3_Standard.sgt", "Mission_Arabs3_Wealthy.sgt", "Mission_Arabs3_Danger.sgt"), // 19
                0x14 => ResolveVariant(variant, "Mission_Midgard1_Standard.sgt", "Mission_Midgard1_Standard.sgt", "Mission_Midgard1_Standard.sgt"), // 20
                0x15 => ResolveVariant(variant, "Mission_Midgard2_Standard.sgt", "Mission_Midgard2_Standard.sgt", "Mission_Midgard2_Standard.sgt"), // 21
                0x1F => ResolveVariant(variant, "Mission_AddOn_Arabs1_Standard.sgt", "Mission_AddOn_Arabs1_Wealthy.sgt", "Mission_AddOn_Arabs1_Danger.sgt"), // 31
                0x20 => ResolveVariant(variant, "Mission_AddOn_Arabs2_Standard.sgt", "Mission_AddOn_Arabs2_Wealthy.sgt", "Mission_AddOn_Arabs2_Danger.sgt"), // 32
                0x21 => ResolveVariant(variant, "Mission_AddOn_Franken1_Standard.sgt", "Mission_AddOn_Franken1_Standard.sgt", "Mission_AddOn_Franken1_Standard.sgt"), // 33
                0x22 => ResolveVariant(variant, "Mission_AddOn_Franken2_Standard.sgt", "Mission_AddOn_Franken2_Wealthy.sgt", "Mission_AddOn_Franken2_Danger.sgt"), // 34
                0x23 => ResolveVariant(variant, "Mission_AddOn_Franken3_Standard.sgt", "Mission_AddOn_Franken3_Wealthy.sgt", "Mission_AddOn_Franken3_Danger.sgt"), // 35
                0x24 => ResolveVariant(variant, "Mission_AddOn_Nordland_Standard.sgt", "Mission_AddOn_Nordland_Standard.sgt", "Mission_AddOn_Nordland_Standard.sgt"), // 36
                0x25 => ResolveVariant(variant, "Mission_AddOn_Underworld_Standard.sgt", "Mission_AddOn_Underworld_Standard.sgt", "Mission_AddOn_Underworld_Standard.sgt"), // 37
                0x26 => ResolveVariant(variant, "Mission_AddOn_Asgard_Standard.sgt", "Mission_AddOn_Asgard_Standard.sgt", "Mission_AddOn_Asgard_Standard.sgt"), // 38
                _ => null,
            };
        }

        internal DmSegmentSlot? FindSlot(int type, int variant)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                DmSegmentSlot slot = _slots[i];
                if (!slot.IsUsed)
                {
                    continue;
                }

                if (slot.Type == type && slot.Variant == variant)
                {
                    return slot;
                }
            }

            return null;
        }

        internal bool StartOrSwitchSegment(int type, int variant, IDmObjectRepository repository, IDmPlaybackBackend backend, uint now)
        {
            Logger.Logger.Info("DmManager", "StartOrSwitchSegment entered. Type=0x" + type.ToString("X2") + ", Variant=" + variant + ", Now=" + now);

            if (!_isInitialized)
            {
                _lastError = "DMManager: not initialized.";
                Logger.Logger.Error("DmManager", _lastError);
                return false;
            }

            if (type < FirstPlayableType || type > LastPlayableType)
            {
                _lastError = "DMManager: invalid music type.";
                Logger.Logger.Error("DmManager", _lastError + " type=0x" + type.ToString("X2"));
                return false;
            }

            if (IsSpecialType(type))
            {
                Logger.Logger.Info("DmManager", "Type is special. Delegating to StartSpecialType.");
                return StartSpecialType(type, backend, now);
            }

            DmSegmentSlot? slot = FindSlot(type, variant);
            Logger.Logger.Info("DmManager", "Existing slot found=" + (slot != null));

            if (slot == null)
            {
                slot = CreateSlot(type, variant, repository, backend);
                if (slot == null)
                {
                    Logger.Logger.Error("DmManager", "CreateSlot failed. LastError='" + (_lastError ?? "<null>") + "'");
                    return false;
                }
            }

            if (!EnsureAudiopath(slot, backend))
            {
                Logger.Logger.Error("DmManager", "EnsureAudiopath failed. LastError='" + (_lastError ?? "<null>") + "'");
                return false;
            }

            bool stateBeforeStartRead = backend.GetPlaybackStateOfSegment(slot.SegmentHandle, out byte stateBeforeStart);
            if (!stateBeforeStartRead)
            {
                Logger.Logger.Warning("DmManager", "Playback state before start could not be read for segment '" + (slot.SegmentName ?? "<null>") + "'. Start will still be attempted because +0x5C is not treated as a hard truth source.");
                stateBeforeStart = 0;
            }
            else
            {
                Logger.Logger.Info("DmManager", "Playback state before start for segment '" + (slot.SegmentName ?? "<null>") + "' => " + stateBeforeStart);
            }

            if (!stateBeforeStartRead || stateBeforeStart == 0)
            {
                bool started = backend.StartSegmentPlayback(_audiopath, slot.SegmentHandle, StartFlags, StartTime, RepeatCount, StartUnknown);
                Logger.Logger.Info("DmManager", "StartSegmentPlayback returned " + started);

                if (!started)
                {
                    _lastError = "DMManager: geStartSegmentPlayback failed.";
                    Logger.Logger.Error("DmManager", _lastError);
                    return false;
                }

                byte stateAfterStart = 0;
                bool stateAfterStartReadAtLeastOnce = false;
                bool stateAfterStartBecameNonZero = false;

                for (int poll = 0; poll < PlaybackStatePollCount; poll++)
                {
                    bool stateRead = backend.GetPlaybackStateOfSegment(slot.SegmentHandle, out stateAfterStart);
                    if (!stateRead)
                    {
                        Logger.Logger.Warning("DmManager", "Playback state poll " + (poll + 1) + "/" + PlaybackStatePollCount + " failed for segment '" + (slot.SegmentName ?? "<null>") + "'.");
                    }
                    else
                    {
                        stateAfterStartReadAtLeastOnce = true;
                        Logger.Logger.Info("DmManager", "Playback state poll " + (poll + 1) + "/" + PlaybackStatePollCount + " for segment '" + (slot.SegmentName ?? "<null>") + "' => " + stateAfterStart);

                        if (stateAfterStart != 0)
                        {
                            stateAfterStartBecameNonZero = true;
                            break;
                        }
                    }

                    Thread.Sleep(PlaybackStatePollSleepMilliseconds);
                }

                if (!stateAfterStartReadAtLeastOnce)
                {
                    Logger.Logger.Warning("DmManager", "Playback state could not be read after start for segment '" + (slot.SegmentName ?? "<null>") + "'. Start is still accepted because audible playback may still be valid.");
                }
                else
                {
                    Logger.Logger.Info("DmManager", "Playback state after start for segment '" + (slot.SegmentName ?? "<null>") + "' => " + stateAfterStart);
                }

                if (!stateAfterStartBecameNonZero)
                {
                    Logger.Logger.Warning("DmManager", "DMManager: segment start call returned success, but playback state never became non-zero during polling.");
                }
            }
            else
            {
                Logger.Logger.Info("DmManager", "Segment '" + (slot.SegmentName ?? "<null>") + "' already appears active. StartSegmentPlayback is skipped.");
            }

            _currentType = type;
            _currentVariant = variant;

            Logger.Logger.Info("DmManager", "StartOrSwitchSegment succeeded. CurrentType=0x" + _currentType.ToString("X2") + ", CurrentVariant=" + _currentVariant);
            return true;
        }

        internal void ResetAllSegmentPlaybackStates(IDmPlaybackBackend backend)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                DmSegmentSlot slot = _slots[i];
                if (!slot.IsUsed)
                {
                    continue;
                }

                bool resetApplied = backend.ResetSegmentPlayback(slot.SegmentHandle, 0);
                Logger.Logger.Info("DmManager", "ResetSegmentPlayback(slot[" + i + "]) returned " + resetApplied + ", value=0, segment='" + (slot.SegmentName ?? "<null>") + "'");

                if (!resetApplied)
                {
                    _lastError = "DMManager: geResetSegmentPlayback failed while clearing segment states.";
                    Logger.Logger.Warning("DmManager", _lastError);
                }
            }

            _currentType = 0;
            _currentVariant = 0;
        }

        internal bool Shutdown(IDmPlaybackBackend backend)
        {
            Logger.Logger.Info("DmManager", "Shutdown entered. _isInitialized=" + _isInitialized);

            if (!_isInitialized)
            {
                Logger.Logger.Warning("DmManager", "Shutdown ignored because manager is not initialized.");
                return false;
            }

            ResetAllSegmentPlaybackStates(backend);

            for (int i = 0; i < _slots.Length; i++)
            {
                DmSegmentSlot slot = _slots[i];
                if (!slot.IsUsed)
                {
                    continue;
                }

                Logger.Logger.Info("DmManager", "Destroying slot[" + i + "] SegmentName='" + (slot.SegmentName ?? "<null>") + "'");

                if (!backend.DestroySegment(slot.SegmentHandle))
                {
                    _lastError = "DMManager: geDestroySegment failed.";
                    Logger.Logger.Error("DmManager", _lastError);
                }

                slot.Clear();
            }

            if (_audiopath != null)
            {
                Logger.Logger.Info("DmManager", "Destroying audiopath.");

                if (!backend.ActivateAudiopath(_audiopath, false))
                {
                    _lastError = "DMManager: geActivateAudiopath failed.";
                    Logger.Logger.Error("DmManager", _lastError);
                }

                if (!backend.DestroyAudiopath(_audiopath))
                {
                    _lastError = "DMManager: geDestroyAudiopath failed.";
                    Logger.Logger.Error("DmManager", _lastError);
                }
            }

            Logger.Logger.Info("DmManager", "Shutting down playback backend via ShutdownPerformance().");
            backend.ShutdownPerformance();

            _audiopath = null;
            _currentType = 0;
            _currentVariant = 0;
            _specialDeadline = 0;
            _specialVolumeApplied = false;
            _isInitialized = false;

            Logger.Logger.Info("DmManager", "Shutdown finished.");
            return true;
        }

        private DmSegmentSlot? CreateSlot(int type, int variant, IDmObjectRepository repository, IDmPlaybackBackend backend)
        {
            Logger.Logger.Info("DmManager", "CreateSlot entered. Type=0x" + type.ToString("X2") + ", Variant=" + variant);

            string? segmentName = ResolveSegmentName(type, variant);
            Logger.Logger.Info("DmManager", "Resolved segmentName='" + (segmentName ?? "<null>") + "'");

            if (string.IsNullOrEmpty(segmentName))
            {
                _lastError = "DMManager: unresolved segment name.";
                Logger.Logger.Error("DmManager", _lastError);
                return null;
            }

            DmSegmentSlot? freeSlot = FindFreeSlot();
            if (freeSlot == null)
            {
                _lastError = "DMManager: No free segment entry found!";
                Logger.Logger.Error("DmManager", _lastError);
                return null;
            }

            IDmObject? dmObject = repository.LoadObject(type, variant, segmentName);
            Logger.Logger.Info("DmManager", "Repository returned object=" + (dmObject != null));

            if (dmObject == null)
            {
                _lastError = "DMManager: repository load failed.";
                Logger.Logger.Error("DmManager", _lastError);
                return null;
            }

            bool loaded = backend.LoadCachedObject(dmObject, _rootPath, out object? segmentHandle, out object? loadedState);
            Logger.Logger.Info("DmManager", "LoadCachedObject returned " + loaded);

            if (!loaded)
            {
                _lastError = "DMManager: geLoadCachedObject failed.";
                Logger.Logger.Error("DmManager", _lastError);
                return null;
            }

            freeSlot.Clear();
            freeSlot.IsUsed = true;
            freeSlot.Type = type;
            freeSlot.Variant = variant;
            freeSlot.SegmentName = segmentName;
            freeSlot.LoadedObject = dmObject;
            freeSlot.SegmentHandle = segmentHandle;
            freeSlot.Field08 = loadedState;

            Logger.Logger.Info("DmManager", "CreateSlot succeeded for '" + segmentName + "'");
            return freeSlot;
        }

        private DmSegmentSlot? FindFreeSlot()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].IsUsed)
                {
                    return _slots[i];
                }
            }

            return null;
        }

        private bool EnsureAudiopath(DmSegmentSlot slot, IDmPlaybackBackend backend)
        {
            if (_audiopath != null)
            {
                Logger.Logger.Debug("DmManager", "Audiopath already exists.");
                return true;
            }

            int audiopathMode = _musicMode == 2 ? 1 : 3;
            object? segmentForAudiopath = _musicMode == 2 ? null : slot.SegmentHandle;

            Logger.Logger.Info("DmManager", "Creating audiopath. MusicMode=" + _musicMode + ", AudiopathMode=" + audiopathMode + ", Config=0x" + _audiopathConfig.ToString("X2") + ", SegmentName='" + (slot.SegmentName ?? "<null>") + "'");

            bool created = backend.CreateAudiopath(audiopathMode, _audiopathConfig, segmentForAudiopath, out object? audiopath);
            Logger.Logger.Info("DmManager", "CreateAudiopath returned " + created);

            if (!created)
            {
                _lastError = "DMManager: geCreateAudiopath failed.";
                Logger.Logger.Error("DmManager", _lastError);
                return false;
            }

            bool activated = backend.ActivateAudiopath(audiopath, true);
            Logger.Logger.Info("DmManager", "ActivateAudiopath returned " + activated);

            if (!activated)
            {
                _lastError = "DMManager: geActivateAudiopath failed.";
                Logger.Logger.Error("DmManager", _lastError);
                backend.DestroyAudiopath(audiopath);
                return false;
            }

            Logger.Logger.Info("DmManager", "Waiting 20 ms after geActivateAudiopath to let native audiopath settle.");
            Thread.Sleep(20);

            _audiopath = audiopath;
            Logger.Logger.Info("DmManager", "EnsureAudiopath succeeded.");
            return true;
        }

        private bool StartSpecialType(int type, IDmPlaybackBackend backend, uint now)
        {
            if (_audiopath != null && !_specialVolumeApplied)
            {
                bool ok = backend.SetVolumeOfAudiopath(_audiopath, FadeDownVolume, FadeDownMilliseconds);
                if (!ok)
                {
                    _lastError = "DMManager: geSetVolumeOfAudiopath failed.";
                    return false;
                }

                _specialDeadline = 0;
                _specialVolumeApplied = true;
            }

            _specialDeadline = now + GetSpecialDuration(type);
            return true;
        }

        private static bool IsSpecialType(int type)
        {
            return type >= FirstSpecialType && type <= LastSpecialType;
        }

        private static uint GetSpecialDuration(int type)
        {
            return SpecialDurations[type];
        }

        private static string ResolveVariant(int variant, string standard, string wealthy, string danger)
        {
            return variant switch
            {
                4 => standard,
                5 => wealthy,
                6 => danger,
                _ => standard,
            };
        }

        private static readonly uint[] SpecialDurations =
        [
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            2800,
            3700,
            5000,
            3200,
            3300,
            8200,
            8000,
            2800,
            3000,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
        ];
    }
}