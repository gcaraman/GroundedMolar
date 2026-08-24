# GroundedMolar

Offline Windows tooling for locating uncollected, randomized Milk Molars selected in a Grounded New Game+ world.

GroundedMolar is a free, unofficial fan project. It isn't endorsed by Obsidian Entertainment, Inc. and doesn't reflect the views or opinions of Obsidian Entertainment, Inc. or anyone officially involved with making Grounded. Grounded, Obsidian Entertainment, and their related marks belong to their respective owners.

This Phase 1 foundation contains domain models, decoder/profile seams, fail-closed analysis, a CLI, and verified projection. Unknown input returns `Unsupported` rather than guessed results.

## Quick start

```powershell
$env:DOTNET_CLI_HOME = "$PWD/.dotnet-cli"
$env:APPDATA = "$PWD/.appdata"
$env:NUGET_PACKAGES = "$PWD/.nuget/packages"
dotnet restore --configfile NuGet.Config
dotnet build --no-restore
dotnet run --project tests/GroundedMolar.Tests
dotnet run --project src/GroundedMolar.Cli -- C:\path\to\World.csav
dotnet run --project src/GroundedMolar.Cli -- C:\path\to\World.csav --decoded-output C:\temp\World.bin
dotnet run --project src/GroundedMolar.Preview -- --build-map C:\path\to\MapIcons marker-free-map.png
dotnet run --project src/GroundedMolar.Preview -- C:\path\to\World.csav C:\path\to\marker-free-map.png preview.png --icon C:\path\to\T_UI_MM_MorselGeneric.png
dotnet run --project src/GroundedMolar.App
```

- `src/GroundedMolar.Core` — core contracts, analysis, projection
- `src/GroundedMolar.Cli` — Phase 1 entry point
- `src/GroundedMolar.Preview` — standalone 4096×4096 PNG preview renderer
- `src/GroundedMolar.App` — WPF save picker, folder monitor, and map-image viewer
- `tests/GroundedMolar.Tests` — dependency-free regression runner
- `docs` — specification, roadmap, evidence log

The project targets .NET 10 and pins SDK 10.0.303 plus Windows Desktop Runtime 10.0.11 as the current minimum secure self-contained release baseline.

## Public Windows release

Run `powershell -ExecutionPolicy Bypass -File scripts/Publish-Nexus.ps1` to build and verify a self-contained Windows x64 ZIP under `artifacts/nexus`. End users extract the ZIP and run `GroundedMolar.exe`; no .NET installation, mod manager, game-file modification, account, or network connection is required. See [docs/NEXUS_RELEASE.md](docs/NEXUS_RELEASE.md) for the prepared Nexus description, permissions, upload checklist, and known limitations.

## Grounded save decoding

The CLI accepts `World.csav` directly. `GroundedCsavDecoder` validates Grounded's little-endian `uint32 decodedSize` / `uint32 compressedSize` header, translates the payload into the container expected by the pinned `ooz v7.0` backend, validates the exact result length, and removes its temporary working directory. It never creates a permanent `_decompressed.bin` file.

The CLI also validates the world's party discovery record and prints its authoritative discovered `POI…` identifiers. This is named map-location discovery state, not an inferred fog-of-war mask or walked-area estimate. An unrecognized discovery layout reports `Unsupported` and returns no POIs.

For local format research, `--decoded-output` writes the validated decoded bytes to an explicit caller-selected path. Private decoded saves remain excluded from the repository and must never be committed.

The bundled `ooz.exe` is integrity-pinned to SHA-256:

`271D3FD02E582175FF033D0A23DCA3785B6888FA21B8CD06741BA8C19B71DF41`

The native decoder is treated as hostile: it runs in a no-capability AppContainer with no network access, read-only access to one private staging directory, an explicit inherited-handle allowlist, and Job Object limits for one process, memory, CPU, and elapsed time. Decoded bytes travel through size-bounded stdout rather than a helper-writable output file, so the helper cannot consume disk beyond the broker-created, quota-checked executable and input snapshots. Any sandbox, helper, quota, crash, or output validation failure returns `Unsupported` and produces no markers.

To run the private fixture integration check locally:

```powershell
$env:GROUNDED_FIXTURE_DIR = "C:\path\to\Grounded\save-folder"
$env:GROUNDED_OOZ_PATH = "$PWD\ooz.exe"
dotnet run --project tests/GroundedMolar.Tests
```

## NG+ molar analysis

`GroundedSaveFormatProfileV1` reconstructs the selected entries from both randomized spawn groups. Each entry is decoded atomically into its Unreal FGuid, persistent actor FGuid, world transform, group, spawn-data class, and group index.

Collection and approach state are resolved only for those selected entries:

- Persistent actor state `0`: unapproached and uncollected.
- Persistent actor state `1`: approached and uncollected.
- Selected actor reference with no remaining persistent record: collected.
- Changed or ambiguous structural signatures: unsupported; no markers.

The CLI prints only authoritative uncollected GUIDs and X/Y/Z after reporting `Confidence: Validated`. Player files and candidate-spawn subtraction are not used.

UI changes should follow [the Grounded-derived UI design guidelines](docs/UI_DESIGN_GUIDELINES.md). They document the evidence boundary, semantic palette and typography, settings control patterns, accessibility requirements, and the recommended WPF adaptation.

## Standalone map preview

`GroundedMolar.Preview` is developer tooling and is explicitly excluded from public release archives. Its caller-selected map/icon inputs are not part of the end-user save-folder trust boundary.

The `--build-map` mode reconstructs the marker-free in-game map from `T_UI_Worldmap_BG.png`, `T_UI_Worldmap_Base.png`, and `T_UI_Worldmap_Water.png`. It applies the required 90° counter-clockwise correction and the palette recovered from the marker-free in-game reference.

The normal preview mode renders a composed 4096×4096 map with every authoritative uncollected record from a `Validated` analysis. Unapproached markers are drawn at 45% opacity and approached markers at full opacity. The renderer uses the accepted screenshot calibration scaled through normalized coordinates. The diagnostic comparison SVG in `preview/` shows that calibration in cyan and the tentative exported-bounds transform in magenta; the tentative transform remains isolated until an in-game ground-truth check resolves the discrepancy.

## Desktop save-map viewer

The WPF app provides a direct `World.csav` picker, an optional recursive saves-folder monitor, a friendly current-save card using the save's own `SaveGameScreenshot`, a native 4096×4096 map-image preview, and a manual refresh button. When monitoring is checked, it chooses the most recently written `World.csav` in the selected folder and automatically refreshes after debounced file changes. The selected save, folder, monitoring choice, and unapproached-marker opacity are restored on the next launch. Marker centers are projected directly from each selected spawn's serialized X/Y transform. Only `Validated` + `Uncollected` records are rendered using the white 64×64 in-game Milk Molar icon.

Select a `World.csav` directly or choose a saves folder, then use **Refresh** whenever an immediate reload is wanted. All uncollected selected spawns are displayed; unapproached molars are slightly transparent. A changed or unrecognized save profile clears the preview rather than guessing.
