# Current DirectMusic / gedx8musicdrv Status

## Confirmed by ASM/C# comparison

- `+0x08` creates the driver instance.
- `+0x10` initializes the synthesizer with the 12-byte config structure.
- `+0x20` loads a cached object and returns the created wrapper at `record + 0x00`.
- `+0x34` creates an audiopath object.
- `+0x38` activates an audiopath.
- `+0x54` starts segment playback using the loaded segment wrapper.
- `+0x5C` queries playback state using the loaded segment wrapper.

The recent runtime log confirms that:
- segment loading succeeds
- audiopath creation succeeds
- playback start succeeds
- playback state changes from `0` to `1`

This means the main playback path is no longer the primary blocker.

## Most likely remaining practical issue

Current successful runtime logs show:

- segment loading succeeds
- audiopath creation succeeds
- audiopath activation succeeds
- playback start succeeds
- playback state changes from `0` to `1`

So the primary playback path is now working far enough that the driver believes the segment is running.

The most likely remaining practical issue is now audiopath-level volume/state initialization on the normal playback path.

In the current C# flow, the normal path creates and activates an audiopath, but does not explicitly normalize its audiopath volume afterward.
The codebase already uses `SetVolumeOfAudiopath(...)` with `0` as the restore/normal level in the special/fade path, which strongly suggests that an explicit `SetVolumeOfAudiopath(audiopath, 0, 0)` should also be applied after successful audiopath activation in the normal path.

## Important source-state note

The uploaded `/mnt` source and the latest successful runtime log are not fully in sync.

The uploaded `Gedx8MusicDriverPlaybackBackend.cs` still contains the previously tested `CreateAudiopath(...)` call shape with:

- arg2 = reserved
- arg3 = out wrapper
- arg4 = config

But the successful runtime log corresponds to the older working call shape where audiopath creation succeeds.
So the uploaded source snapshot should be treated as partially stale relative to the latest local run.

## Revised +0x34 audiopath mapping

The previous note claiming that argument 2 is unused and argument 4 is the config pointer was incorrect.

Current ASM review of `10001B70` shows:

- argument 3 is the output pointer receiving the created audiopath wrapper
- argument 4 is an optional wrapper-like object, because the wrapper checks `[arg4+4]` and consumes `[arg4+0x0C]`
- the working C# call shape is therefore consistent with:
  - arg1 = driver instance
  - arg2 = audiopath config pointer
  - arg3 = out audiopath pointer
  - arg4 = optional segment wrapper

This matches the earlier runtime behavior where audiopath creation succeeded, while the later modified call shape caused immediate `geCreateAudiopath failed`.