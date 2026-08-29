# MolarMap

Offline Windows tooling for locating uncollected, randomized Milk Molars selected in a Grounded New Game+ world.

MolarMap is a free, unofficial fan project. It isn't endorsed by Obsidian Entertainment, Inc. and doesn't reflect the views or opinions of Obsidian Entertainment, Inc. or anyone officially involved with making Grounded. Grounded, Obsidian Entertainment, and their related marks belong to their respective owners.

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
dotnet run --project src/GroundedMolar.App
```

- `src/GroundedMolar.Core` — core contracts, analysis, projection
- `src/GroundedMolar.Cli` — Phase 1 entry point
- `src/GroundedMolar.App` — WPF save picker, folder monitor, and map-image viewer
- `tests/GroundedMolar.Tests` — dependency-free regression runner
- `docs` — specification, roadmap, evidence log

The project targets .NET 10 and pins SDK 10.0.303 plus Windows Desktop Runtime 10.0.11 as the current minimum secure self-contained release baseline.

## Public Windows release

Version 1.0.1 uses a Store-signed x64 MSIX for Microsoft Store and a separately signed offline x64 MSI inside the Nexus/direct-download ZIP. Both channels must be built from the same source revision with the same product version and user-visible release notes, but the unsigned pre-certification MSIX must never be distributed through Nexus. Run `scripts/Publish-StoreMsix.ps1` for the Store submission package and `scripts/Publish-Release.ps1` with a publicly trusted signing identity for the Nexus/direct release. Running the latter without signing parameters creates only clearly labeled `UNSIGNED-DO-NOT-PUBLISH` rehearsal artifacts. See [docs/RELEASE_1.0.1.md](docs/RELEASE_1.0.1.md), [docs/NEXUS_RELEASE.md](docs/NEXUS_RELEASE.md), and [docs/MICROSOFT_STORE_RELEASE.md](docs/MICROSOFT_STORE_RELEASE.md).

End users run the MSI and launch MolarMap from the Start menu; no separate .NET installation, mod manager, game-file modification, account, or network connection is required.

## Grounded save decoding

The CLI accepts `World.csav` directly. `GroundedCsavDecoder` validates Grounded's little-endian `uint32 decodedSize` / `uint32 compressedSize` header, translates the payload into the container expected by the pinned `ooz v7.0` backend, validates the exact result length, and removes its temporary working directory. It never creates a permanent `_decompressed.bin` file.

The CLI also validates the world's party discovery record and prints its authoritative discovered `POI…` identifiers. This is named map-location discovery state, not an inferred fog-of-war mask or walked-area estimate. An unrecognized discovery layout reports `Unsupported` and returns no POIs.

For local format research, `--decoded-output` writes the validated decoded bytes to an explicit caller-selected path. Private decoded saves remain excluded from the repository and must never be committed.

The repository's reviewed upstream `ooz.exe` is integrity-pinned to SHA-256:

`271D3FD02E582175FF033D0A23DCA3785B6888FA21B8CD06741BA8C19B71DF41`

Trusted releases sign a staged copy before compilation. Signing changes its bytes, so the release pipeline compiles the signed helper's exact SHA-256 into `GroundedMolar.Core.dll` and records it in `release-manifest.json`.

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

## Desktop save-map viewer

The WPF app provides a direct `World.csav` picker, an optional recursive saves-folder monitor, a native logical 512 × 512 map-image preview, a friendly current-save card using the save's own `SaveGameScreenshot`, and a manual refresh button. When monitoring is checked, it chooses the most recently written `World.csav` in the selected folder and automatically refreshes after debounced file changes. The selected save, folder, monitoring choice, and unapproached-marker opacity are restored on the next launch. Marker centers are projected directly from each selected spawn's serialized X/Y transform. Only `Validated` + `Uncollected` records are rendered using the white 64×64 in-game Milk Molar icon.

Select a `World.csav` directly or choose a saves folder, then use **Refresh** whenever an immediate reload is wanted. All uncollected selected spawns are displayed; unapproached molars are slightly transparent. Enable **Show community guide hints** in Preferences to reveal the bundled community location hint in every marker popup. The same preference card links to the source guide and requires confirmation of the full external URL before opening a browser. The editable guide catalog is [`src/GroundedMolar.App/Data/milk-molar-guide.json`](src/GroundedMolar.App/Data/milk-molar-guide.json); rebuild or republish after editing it. Hints are approximate presentation data and never affect save validation or marker state. A changed or unrecognized save profile clears the preview rather than guessing.
