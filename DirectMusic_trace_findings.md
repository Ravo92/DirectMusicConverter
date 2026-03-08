# DirectMusic / gedx8musicdrv findings used for the patch

- `GetInterface2` returns a raw interface object whose method table is read from `raw + 4`.
- Method table slot `+0x08` is the driver-instance creation call.
- Method table slot `+0x10` is the synthesizer init call and takes `(driverInstance, &config12)`.
- The init config is a 12-byte structure: `reserved`, `sampleRate`, `config`.
- Observed mappings:
  - mode 0/default -> `44100`, `0x40`
  - mode 1 -> `22050`, `0x10`
  - mode 2 -> `11025`, `0x08`
- The path setup inside the loader path matches `IDirectMusicLoader8::SetSearchDirectory(GUID_DirectMusicAllTypes, widePath, FALSE)`.
- The object-load path matches the usual DirectMusic pattern of:
  - set search directory
  - load a segment using class/interface GUIDs
  - file name is passed separately from the base directory
- In the game trace, the GUID block at `154AC328` matched `GUID_DirectMusicAllTypes`.
- The proprietary `+0x20` wrapper appears to take:
  - driver instance
  - mode / flags
  - object descriptor pointer
  - output wrapper pointer
  - base path (ANSI)

## Remaining uncertainty

The exact native output layout written by the proprietary `+0x20` load wrapper is still not fully proven. The patch therefore uses a best-fit interpretation of the temporary descriptor/output buffer layout that matched the observed trace.
