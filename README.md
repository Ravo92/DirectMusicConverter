# Remaining Reverse-Engineering Gaps for Real Playback

The following gaps still need to be closed before the current C# player can load and play `.sgt` files end-to-end.

## Open issues

1. **The exact game/DLL code path that creates the loader context used by `geLoadCachedObject(..., loaderContext)`**
   - The latest game-side initialization trace shows that method-table entry `+0x08` is `geCreateInstance`, not a loader-context constructor.
   - The current code therefore can no longer treat `+0x08` as `CreateLoaderContext()`.

2. **The exact signature of the loader-context creation/open call**
   - Current candidates include the wrappers around DLL offsets `+0x20` and `+0x58`, as well as the type-0 object path inside `FUN_10003890`, `FUN_10004120`, and `FUN_10004670`.

3. **The exact shutdown/release methods for the created driver instance and loader-related objects**
   - The previously assumed shutdown offsets (`+0x0C`, `+0x14`, `+0x30`) are not confirmed by the current evidence.

4. **Confirmation of the playback-side signatures for the following still-unverified method-table entries**
   - `+0x34` — create audiopath
   - `+0x38` — activate/select/property-dispatch interaction
   - `+0x3C` — set volume
   - `+0x50` — destroy audiopath
   - `+0x54` — start/release/unload ambiguity
   - `+0x58` — reset/open-resolve ambiguity
   - `+0x5C` — playback-state/query ambiguity
   - `+0x6C` — destroy-segment / stub ambiguity

## Confirmed finding relevant to the remaining work

5. **`gedx8musicdrv.dll` method-table entry `+0x20`: confirmed working call shape for segment loading**
   - `geLoadCachedObject(driverInstance, mode, descriptorPtr, outWrapperPtr, basePathAnsi)`

For `.sgt` segment loading:
- `descriptorPtr = record + 0x04`
- `outWrapperPtr = record + 0x00`
- `record + 0x04 = 0` (segment/object kind for this path)
- `record + 0x08 = ANSI filename pointer`
- `basePathAnsi` is passed separately as the final argument

Previous assumptions were incorrect:
- the wrapper is **not** returned in `record + 0x04`
- type/variant are **not** the native descriptor fields for this call path

Observed success pattern:
- the DLL writes the wrapper pointer to `record + 0x00`
- `record + 0x04` remains a descriptor field, not a wrapper output field

---

# DirectMusic / gedx8musicdrv Findings Used for the Patch

- `GetInterface2` returns a raw interface object whose method table is read from `raw + 4`.
- Method-table slot `+0x08` is the driver-instance creation call.
- Method-table slot `+0x10` is the synthesizer-init call and takes `(driverInstance, &config12)`.
- The init config is a 12-byte structure consisting of:
  - `reserved`
  - `sampleRate`
  - `config`

## Observed init mappings

- mode 0 / default -> `44100`, `0x40`
- mode 1 -> `22050`, `0x10`
- mode 2 -> `11025`, `0x08`

## Loader-path findings

- The path setup inside the loader path matches `IDirectMusicLoader8::SetSearchDirectory(GUID_DirectMusicAllTypes, widePath, FALSE)`.
- The object-load path matches the usual DirectMusic pattern:
  - set search directory
  - load a segment using class/interface GUIDs
  - pass the file name separately from the base directory
- In the game trace, the GUID block at `154AC328` matched `GUID_DirectMusicAllTypes`.
- The proprietary `+0x20` wrapper appears to take:
  - driver instance
  - mode / flags
  - object descriptor pointer
  - output wrapper pointer
  - base path (ANSI)

## Remaining uncertainty

The exact native output layout written by the proprietary `+0x20` load wrapper is still not fully proven. The patch therefore uses a best-fit interpretation of the temporary descriptor/output buffer layout that matched the observed trace.