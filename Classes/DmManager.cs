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

        internal object? Audiopath => _audiopath;

        internal int CurrentType => _currentType;

        internal int CurrentVariant => _currentVariant;

        internal uint SpecialDeadline => _specialDeadline;

        internal bool SpecialVolumeApplied => _specialVolumeApplied;

        internal IReadOnlyList<DmSegmentSlot> Slots => _slots;

        internal void MarkInitialized()
        {
            _isInitialized = true;
        }

        internal void SetRootPath(string? rootPath)
        {
            _rootPath = rootPath;
        }

        internal void SetMusicMode(int musicMode)
        {
            _musicMode = musicMode;
        }

        internal void SetAudiopathConfig(int audiopathConfig)
        {
            _audiopathConfig = audiopathConfig;
        }

        internal string? ResolveSegmentName(int type, int variant)
        {
            if (type < FirstPlayableType || type > LastPlayableType)
            {
                return null;
            }

            return type switch
            {
                0x03 => ResolveVariant(variant, "Theme_Viking_Friendly.sgt", "Theme_Viking_Neutral.sgt", "Theme_Viking_Hostile.sgt"),
                0x04 => ResolveVariant(variant, "Theme_Franken_Friendly.sgt", "Theme_Franken_Neutral.sgt", "Theme_Franken_Hostile.sgt"),
                0x06 => "Attack_Viking.sgt",
                0x07 => "Attack_Franken.sgt",
                0x08 => "Attack_Byzanz.sgt",
                0x09 => "Attack_Arabs.sgt",
                0x0A => ResolveVariant(variant, "Mission_Viking1_Standard.sgt", "Mission_Viking1_Wealthy.sgt", "Mission_Viking1_Danger.sgt"),
                0x0B => ResolveVariant(variant, "Mission_Franken1_Standard.sgt", "Mission_Franken1_Wealthy.sgt", "Mission_Franken1_Danger.sgt"),
                0x0C => ResolveVariant(variant, "Mission_Franken2_Standard.sgt", "Mission_Franken2_Wealthy.sgt", "Mission_Franken2_Danger.sgt"),
                0x0D => ResolveVariant(variant, "Mission_Byzanz1_Standard.sgt", "Mission_Byzanz1_Wealthy.sgt", "Mission_Byzanz1_Danger.sgt"),
                0x0E => ResolveVariant(variant, "Mission_Byzanz2_Standard.sgt", "Mission_Byzanz2_Wealthy.sgt", "Mission_Byzanz2_Danger.sgt"),
                0x0F => ResolveVariant(variant, "Mission_Byzanz3_Standard.sgt", "Mission_Byzanz3_Wealthy.sgt", "Mission_Byzanz3_Danger.sgt"),
                0x10 => ResolveVariant(variant, "Mission_Byzanz4_Standard.sgt", "Mission_Byzanz4_Wealthy.sgt", "Mission_Byzanz4_Danger.sgt"),
                0x11 => ResolveVariant(variant, "Mission_Arabs1_Standard.sgt", "Mission_Arabs1_Wealthy.sgt", "Mission_Arabs1_Danger.sgt"),
                0x12 => ResolveVariant(variant, "Mission_Arabs2_Standard.sgt", "Mission_Arabs2_Wealthy.sgt", "Mission_Arabs2_Danger.sgt"),
                0x13 => ResolveVariant(variant, "Mission_Arabs3_Standard.sgt", "Mission_Arabs3_Wealthy.sgt", "Mission_Arabs3_Danger.sgt"),
                0x14 => ResolveVariant(variant, "Mission_Midgard1_Standard.sgt", "Mission_Midgard1_Standard.sgt", "Mission_Midgard1_Standard.sgt"),
                0x15 => ResolveVariant(variant, "Mission_Midgard2_Standard.sgt", "Mission_Midgard2_Standard.sgt", "Mission_Midgard2_Standard.sgt"),
                0x1F => ResolveVariant(variant, "Mission_AddOn_Arabs1_Standard.sgt", "Mission_AddOn_Arabs1_Wealthy.sgt", "Mission_AddOn_Arabs1_Danger.sgt"),
                0x20 => ResolveVariant(variant, "Mission_AddOn_Arabs2_Standard.sgt", "Mission_AddOn_Arabs2_Wealthy.sgt", "Mission_AddOn_Arabs2_Danger.sgt"),
                0x21 => ResolveVariant(variant, "Mission_AddOn_Franken1_Standard.sgt", "Mission_AddOn_Franken1_Standard.sgt", "Mission_AddOn_Franken1_Standard.sgt"),
                0x22 => ResolveVariant(variant, "Mission_AddOn_Franken2_Standard.sgt", "Mission_AddOn_Franken2_Wealthy.sgt", "Mission_AddOn_Franken2_Danger.sgt"),
                0x23 => ResolveVariant(variant, "Mission_AddOn_Franken3_Standard.sgt", "Mission_AddOn_Franken3_Wealthy.sgt", "Mission_AddOn_Franken3_Danger.sgt"),
                0x24 => ResolveVariant(variant, "Mission_AddOn_Nordland_Standard.sgt", "Mission_AddOn_Nordland_Standard.sgt", "Mission_AddOn_Nordland_Standard.sgt"),
                0x25 => ResolveVariant(variant, "Mission_AddOn_Underworld_Standard.sgt", "Mission_AddOn_Underworld_Standard.sgt", "Mission_AddOn_Underworld_Standard.sgt"),
                0x26 => ResolveVariant(variant, "Mission_AddOn_Asgard_Standard.sgt", "Mission_AddOn_Asgard_Standard.sgt", "Mission_AddOn_Asgard_Standard.sgt"),
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
            if (!_isInitialized)
            {
                _lastError = "DMManager: not initialized.";
                return false;
            }

            if (type < FirstPlayableType || type > LastPlayableType)
            {
                _lastError = "DMManager: invalid music type.";
                return false;
            }

            if (IsSpecialType(type))
            {
                return StartSpecialType(type, backend, now);
            }

            DmSegmentSlot? slot = FindSlot(type, variant);
            if (slot == null)
            {
                slot = CreateSlot(type, variant, repository, backend);
                if (slot == null)
                {
                    return false;
                }
            }

            if (!EnsureAudiopath(slot, backend))
            {
                return false;
            }

            if (!backend.GetPlaybackStateOfSegment(slot.SegmentHandle, out byte state))
            {
                _lastError = "DMManager: geGetPlaybackStateOfSegment failed.";
                return false;
            }

            if (state == 0)
            {
                bool started = backend.StartSegmentPlayback(_audiopath, slot.SegmentHandle, StartFlags, StartTime, RepeatCount, StartUnknown);
                if (!started)
                {
                    _lastError = "DMManager: geStartSegmentPlayback failed.";
                    return false;
                }
            }

            _currentType = type;
            _currentVariant = variant;
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

                backend.ResetSegmentPlayback(slot.SegmentHandle, 0);
            }

            _currentType = 0;
            _currentVariant = 0;
        }

        internal void UpdateSpecialState(IDmPlaybackBackend backend, uint now)
        {
            if (!_specialVolumeApplied)
            {
                return;
            }

            if (_specialDeadline == 0)
            {
                return;
            }

            if (now < _specialDeadline)
            {
                return;
            }

            _specialDeadline = 0;
            _specialVolumeApplied = false;
            backend.SetVolumeOfAudiopath(_audiopath, 0, FadeDownMilliseconds);
        }

        internal bool Shutdown(IDmPlaybackBackend backend)
        {
            if (!_isInitialized)
            {
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

                if (!backend.DestroySegment(slot.SegmentHandle))
                {
                    _lastError = "DMManager: geDestroySegment failed.";
                }

                slot.Clear();
            }

            if (_audiopath != null)
            {
                if (!backend.ActivateAudiopath(_audiopath, false))
                {
                    _lastError = "DMManager: geActivateAudiopath failed.";
                }

                if (!backend.DestroyAudiopath(_audiopath))
                {
                    _lastError = "DMManager: geDestroyAudiopath failed.";
                }
            }

            backend.ShutdownPerformance();
            backend.ShutdownLoader();
            backend.ShutdownDriver();

            _audiopath = null;
            _currentType = 0;
            _currentVariant = 0;
            _specialDeadline = 0;
            _specialVolumeApplied = false;
            _isInitialized = false;
            return true;
        }

        private DmSegmentSlot? CreateSlot(int type, int variant, IDmObjectRepository repository, IDmPlaybackBackend backend)
        {
            string? segmentName = ResolveSegmentName(type, variant);
            if (string.IsNullOrEmpty(segmentName))
            {
                _lastError = "DMManager: unresolved segment name.";
                return null;
            }

            DmSegmentSlot? freeSlot = FindFreeSlot();
            if (freeSlot == null)
            {
                _lastError = "DMManager: No free segment entry found!";
                return null;
            }

            IDmObject? dmObject = repository.LoadObject(type, variant, segmentName);
            if (dmObject == null)
            {
                _lastError = "DMManager: repository load failed.";
                return null;
            }

            bool loaded = backend.LoadCachedObject(dmObject, _rootPath, out object? segmentHandle, out object? loadedState);
            if (!loaded)
            {
                _lastError = "DMManager: geLoadCachedObject failed.";
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
                return true;
            }

            int audiopathMode = _musicMode == 2 ? 1 : 3;
            object? segmentForAudiopath = _musicMode == 2 ? null : slot.SegmentHandle;
            bool created = backend.CreateAudiopath(audiopathMode, _audiopathConfig, segmentForAudiopath, out object? audiopath);
            if (!created)
            {
                _lastError = "DMManager: geCreateAudiopath failed.";
                return false;
            }

            bool activated = backend.ActivateAudiopath(audiopath, true);
            if (!activated)
            {
                _lastError = "DMManager: geActivateAudiopath failed.";
                backend.DestroyAudiopath(audiopath);
                return false;
            }

            _audiopath = audiopath;
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
