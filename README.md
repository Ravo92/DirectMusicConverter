# DirectMusicConverter

DirectMusicConverter is a Windows console application that plays the DirectMusic
`.sgt` soundtrack used by *Cultures: 8th Wonder of the World*. It uses the
reverse-engineered `GetInterface2` ABI of `gedx8musicdrv.dll` and the original
DirectMusic data below the game's `data\dm2` directory.

Despite its historical name, the application is a playback and integration
tool. It does not convert `.sgt` files into another audio format.

## Project status

The application is complete for its intended purpose. The normal music path has
been implemented and verified in practice:

- load the native music driver and obtain its method table;
- create a driver instance and initialize DirectMusic;
- configure the DirectMusic loader search directory;
- load the selected `.sgt` segment;
- create and activate its audio path;
- start looping playback;
- query playback state; and
- stop playback and shut down the native resources.

The synthesizer ABI uses the correct 12-byte x86 field order:

1. window handle;
2. voice/PChannel count; and
3. sample rate.

## Requirements

- Windows with the legacy DirectMusic runtime available;
- .NET 10 SDK or the corresponding .NET 10 runtime for a prebuilt executable;
- a matching native driver DLL; and
- an installed copy of the game containing `data\dm2`.

The original game and its original `gedx8musicdrv.dll` are 32-bit. The x86 build
is therefore the recommended configuration for original-game compatibility.

The executable selects the driver name from its own process architecture:

| Process architecture | Driver filename |
| --- | --- |
| x86 | `gedx8musicdrv.dll` |
| x64 | `Gedx8MusicDriver.dll` |

The process architecture, DLL architecture and selected build configuration
must match.

## Build

Build the recommended x86 configuration from the project directory:

```powershell
dotnet build DirectMusicConverter.csproj -c Release -p:Platform=x86
```

An x64 configuration is also defined for use with the matching x64 driver:

```powershell
dotnet build DirectMusicConverter.csproj -c Release -p:Platform=x64
```

## Usage

```text
DirectMusicConverter.exe [gameRoot] [type] [variant] [driverDirectory] [synthMode] [masterVolume]
```

All arguments are positional. Numeric arguments are parsed as decimal integers;
do not pass values using a `0x` prefix.

| Position | Parameter | Default | Description |
| ---: | --- | --- | --- |
| 1 | `gameRoot` | Current directory | Game installation directory. The program expects `data\dm2` below this path. |
| 2 | `type` | `3` | Music selection ID. See the table below. |
| 3 | `variant` | `4` | Arrangement variant: `4`, `5` or `6`. |
| 4 | `driverDirectory` | `gameRoot` | Directory containing the architecture-matching native driver DLL. |
| 5 | `synthMode` | `0` | Synthesizer and audio-path mode: `0`, `1` or `2`. |
| 6 | `masterVolume` | `100` | Master volume in percent. Values are clamped to `0` through `100`. |

Quote paths that contain spaces.

### Example

Play the friendly Viking theme with the full-quality synthesizer mode:

```powershell
DirectMusicConverter.exe "E:\Steam\steamapps\common\Cultures 8th Wonder" 3 4 "E:\Steam\steamapps\common\Cultures 8th Wonder" 0 100
```

The same invocation through `dotnet run` is:

```powershell
dotnet run --project DirectMusicConverter.csproj -c Release -p:Platform=x86 -- "E:\Steam\steamapps\common\Cultures 8th Wonder" 3 4 "E:\Steam\steamapps\common\Cultures 8th Wonder" 0 100
```

Playback continues until ENTER is pressed.

## Variant parameter

The `variant` parameter selects the arrangement for themes and missions:

| Value | Theme name | Mission name |
| ---: | --- | --- |
| `4` | Friendly | Standard |
| `5` | Neutral | Wealthy |
| `6` | Hostile | Danger |

Attack tracks and missions that only have a Standard arrangement ignore the
distinction because every variant resolves to the same file. Any value other
than `4`, `5` or `6` falls back to Friendly/Standard.

## Synthesizer modes

| `synthMode` | Voices/PChannels | Sample rate | Audio-path behavior |
| ---: | ---: | ---: | --- |
| `0` | 64 | 44,100 Hz | Uses the audio-path configuration embedded in the segment. |
| `1` | 16 | 22,050 Hz | Uses the audio-path configuration embedded in the segment. |
| `2` | 8 | 11,025 Hz | Uses a standard driver-created audio path. |

Mode `0` is the recommended setting and provides the highest playback quality.
Modes `1` and `2` intentionally reduce sample rate and polyphony to reproduce
the legacy quality modes.

## Music type parameter

The following decimal values resolve to playable `.sgt` segments:

| Decimal | Hex | Selection |
| ---: | ---: | --- |
| `3` | `0x03` | Viking theme |
| `4` | `0x04` | Franken theme |
| `6` | `0x06` | Viking attack |
| `7` | `0x07` | Franken attack |
| `8` | `0x08` | Byzantine attack |
| `9` | `0x09` | Arab attack |
| `10` | `0x0A` | Viking mission 1 |
| `11` | `0x0B` | Franken mission 1 |
| `12` | `0x0C` | Franken mission 2 |
| `13` | `0x0D` | Byzantine mission 1 |
| `14` | `0x0E` | Byzantine mission 2 |
| `15` | `0x0F` | Byzantine mission 3 |
| `16` | `0x10` | Byzantine mission 4 |
| `17` | `0x11` | Arab mission 1 |
| `18` | `0x12` | Arab mission 2 |
| `19` | `0x13` | Arab mission 3 |
| `20` | `0x14` | Midgard mission 1 |
| `21` | `0x15` | Midgard mission 2 |
| `31` | `0x1F` | Add-on Arab mission 1 |
| `32` | `0x20` | Add-on Arab mission 2 |
| `33` | `0x21` | Add-on Franken mission 1 |
| `34` | `0x22` | Add-on Franken mission 2 |
| `35` | `0x23` | Add-on Franken mission 3 |
| `36` | `0x24` | Add-on Nordland mission |
| `37` | `0x25` | Add-on Underworld mission |
| `38` | `0x26` | Add-on Asgard mission |

Values `22` through `30` are internal timed control events from the original
music manager. They do not select a standalone segment and should not be used
as the initial CLI playback type. Other unmapped values fail with an unresolved
segment error.

## Playback behavior

The working playback sequence is:

1. initialize the driver and DirectMusic performance;
2. load the requested segment from `data\dm2`;
3. create and activate the audio path;
4. normalize the audio-path volume to native value `0` (no attenuation);
5. wait 20 ms for the native audio path to settle;
6. start the segment with looping enabled; and
7. poll the native playback state for up to approximately 800 ms.

The short settling delay and state polling are intentional integration behavior
for the asynchronous DirectMusic graph. A successful native return value does
not necessarily mean that the entire graph is immediately ready.

## Logging and exit codes

Detailed diagnostics are written to `directmusic_debug.log` next to the running
application.

| Exit code | Meaning |
| ---: | --- |
| `0` | Playback completed and shutdown finished. |
| `2` | Game directory or `data\dm2` was not found. |
| `3` | Driver, DirectMusic or loader initialization failed. |
| `4` | Setting the master volume failed. |
| `5` | Loading or starting the selected music failed. |

Errors are also printed to the console. The log contains the selected mode,
resolved segment, native method pointers, HRESULT values and playback-state
diagnostics.