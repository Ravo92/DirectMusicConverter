# DirectMusic / gedx8musicdrv Status

This document summarizes the current reverse-engineering and integration status of `gedx8musicdrv.dll` and the DirectMusic playback path used by the project.

## What currently works

The main playback path is working.

The following steps are currently confirmed by ASM review and runtime behavior:

- `+0x08` creates the driver instance
- `+0x10` initializes the synthesizer using the 12-byte init structure
- `+0x20` loads a cached object and returns the created wrapper
- `+0x34` creates an audiopath object
- `+0x38` activates an audiopath object
- `+0x3C` is currently used as the normal audiopath state / volume-style call and is exposed as `SetVolumeOfAudiopath(...)`
- `+0x54` starts segment playback using the loaded segment wrapper
- `+0x5C` queries playback state using the loaded segment wrapper

In practical terms, the project can now:

- load `.sgt` segments
- create and activate an audiopath
- start segment playback
- play music successfully in stable runs

## Important practical finding

The biggest remaining issue was **not** the basic playback path itself, but **native timing / settling**.

The driver can report success for:

- segment loading
- audiopath creation
- audiopath activation
- playback start

while the final audio output is still not fully ready.

Without extra settling time, this led to inconsistent behavior such as:

- no audible sound
- wrong instruments
- playback state reporting `0` while sound was already audible
- playback state reporting `1` while the final output was still incorrect

## Current stable workaround

Playback became stable after introducing explicit settling time at key transition points:

- wait after `geLoadCachedObject`
- wait after `geActivateAudiopath`
- poll playback state longer after `geStartSegmentPlayback`

These waits are currently required for stable playback and should be treated as part of the practical integration, not as arbitrary hacks.

## Current synthesizer init path

The currently reliable synthesizer init path is the fixed legacy mapping:

- mode `0` -> `44100`, `0x40`
- mode `1` -> `22050`, `0x10`
- mode `2` -> `11025`, `0x08`

Although the DLL accepts a sample rate through the 12-byte init structure, the known-good runtime path currently remains the fixed legacy mapping above.

## Normal playback path

The normal playback path should currently do the following:

1. create the audiopath
2. activate the audiopath
3. normalize the audiopath state with:

```csharp
SetVolumeOfAudiopath(audiopath, 0, 0);
```

4. wait for the native pipeline to settle
5. start segment playback
6. poll playback state long enough for the native graph to stabilize

## Known limitations

A few parts are still only partially understood.

### `+0x58`

`+0x58` is a real segment-wrapper method, but it is **not** part of the reliable normal startup sequence.

Calling it at the wrong point can push playback back into a non-running state, so it should not be inserted into the standard start path without a clearly confirmed reason.

### `+0x40`, `+0x44`, `+0x48`

Additional wrappers exist around:

- `+0x40`
- `+0x44`
- `+0x48`

These appear to belong to an audiopath property / dispatcher layer, but their exact semantics are still not fully confirmed.

They are not required for the currently working playback path and should be treated as future reverse-engineering targets rather than mandatory startup calls.

## Segment wrapper notes

The loaded segment wrapper currently behaves consistently enough to rule out a simple pointer-corruption explanation.

Observed stable fields:

- `wrapper + 0x04 = 0`
- `wrapper + 0x08 = 2`
- `wrapper + 0x0C = native payload pointer used by the start / reset / state wrappers`

Differences in wrapper addresses such as `...0700` vs `...0788` are not, by themselves, evidence of corruption. They are more likely normal allocation differences or different internal native states.

## What this means overall

The core playback chain is now understood well enough to work.

The remaining fragility was mainly caused by the fact that successful native return values do **not** mean the entire DirectMusic playback graph is already fully ready.

The current practical solution is therefore:

- keep the known-good fixed synth init path
- keep `SetVolumeOfAudiopath(audiopath, 0, 0)` in the normal path
- keep the timing delays and longer playback-state polling
- document the waits clearly in code comments as required native settling behavior

## Next steps

Recommended future work:

- clean up the timing constants and centralize them
- document the stable startup sequence directly in code
- continue reverse-engineering `+0x40 / +0x44 / +0x48`
- further investigate instrument / dependency binding behavior for full determinism