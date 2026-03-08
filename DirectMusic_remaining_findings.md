# Remaining reverse-engineering gaps for real playback

These items are still needed before the current C# player can load and play `.sgt` files end-to-end:

1. The exact game/DLL path that creates the loader context used by `geLoadCachedObject(..., loaderContext)`.
   - The latest game-side init function proves that method table `+0x08` is `geCreateInstance`, not a loader-context constructor.
   - The current code therefore cannot keep using `+0x08` as `CreateLoaderContext()`.

2. The exact signature for the loader-context creation/open call.
   - Candidates are the wrappers currently mapped around DLL offsets `+0x20`, `+0x58`, or the type-0 object path inside `FUN_10003890` / `FUN_10004120` / `FUN_10004670`.

3. The exact shutdown/release methods for the created driver instance and loader-related objects.
   - The previously used shutdown offsets (`+0x0C`, `+0x14`, `+0x30`) are not confirmed by the current evidence.

4. Confirmation of the playback-side signatures for these still-unverified entries:
   - `+0x34` create audiopath
   - `+0x38` activate/select/property dispatch interaction
   - `+0x3C` set volume
   - `+0x50` destroy audiopath
   - `+0x54` start/release/unload ambiguity
   - `+0x58` reset/open-resolve ambiguity
   - `+0x5C` playback-state/query ambiguity
   - `+0x6C` destroy segment / stub ambiguity

5. One successful in-game trace of a real `.sgt` load path after init.
   - A breakpoint sequence around game calls into the driver after `geInitSynthesizer` would likely reveal the missing loader-context builder and the exact parameter order for `geLoadCachedObject`.
