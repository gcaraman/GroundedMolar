# Evidence and decisions

## Full guide audit and eastern laser correction (2026-09-12)

The 219-entry catalog was compared with live source revision 50346: all coordinates/categories and whitespace-normalized descriptions agree. The source itself contains the numbering typo corrected locally from BBQ-wall #14 to #140, corroborated by its attached image title. Number coverage 1–219 is now enforced by the loader. Existing user-reviewed pot numbering corrections remain. The user's left eastern-laser identification and atomic save transforms establish separate #34/#35 GUID bindings in the JSON; no per-molar runtime constants were added.

The reproducible `scripts/Audit-MolarHints.ps1` scan uses production decoding, validated selected records and the production hint lookup, and writes only ignored reports. The completed scan covered 199 distinct spawns from 106 validated saves across 14 worlds; 46 unsupported saves and 10 decode failures contributed no observations. Four duplicate-hint groups and other uncertain matches remain flagged, not forcibly assigned. Source agreement does not prove a spawn-to-description mapping. Full evidence, coverage, limitations and review candidates are in [MOLAR_GUIDE_AUDIT.md](MOLAR_GUIDE_AUDIT.md).

## Nexus 1.0.1 distribution decision

The Nexus Mods File Submission Guidelines were re-checked on 2026-08-29 (the page reported a 2026-08-04 update). Nexus permits functional executables and utilities; it does not require MolarMap to fall back to the portable ZIP solely because the release contains an installer. The relevant restrictions are satisfied by design: MolarMap is functional, offline, non-destructive, does not auto-update or otherwise communicate over the internet, and is submitted with accurate requirements and caveats. Nexus still virus-scans uploaded files.

Nexus distribution for 1.0.1 therefore remains the ZIP containing the signed x64 MSI, supporting notices, release notes, and SHA-256 manifest produced by `scripts/Publish-Release.ps1`. This archive wrapper is the Nexus upload artifact; the signed MSI inside is the installation trust boundary. The portable 1.0.0-style application ZIP is only the fallback if Nexus later rejects installer-bearing archives.

Publication was not attempted on 2026-08-29 because no publicly trusted code-signing identity was available in either Windows certificate store or the configured environment. The only generated 1.0.1 artifacts are an unsigned rehearsal whose manifest says `productionReady: false` and whose filenames say `UNSIGNED-DO-NOT-PUBLISH`. Uploading or renaming that rehearsal would violate the release gates. The repository build passed, but two consecutive standard verification runs also failed four production-sandbox integration checks when Windows returned `CreateProcess(AppContainer)` error 2; deterministic checks passed and private-fixture checks were unavailable. Publishing remains blocked until the signed production pipeline, complete verification, malware scan, and clean-machine checks in `docs/RELEASE_1.0.1.md` pass.

Profile rejection diagnostics retain the exact structural validation message while continuing to return `Unsupported` with no markers. Diagnostics identify the profile and failed invariant only; they do not include decoded save bytes or private record contents.

Either selected NG+ molar group, or both groups, may have a serialized count of zero after all corresponding molars are collected. A zero-count group ends immediately after its count and has no reserved field or entry records. The exact normal and underwater group records are still required exactly once; zero selected entries validate and render zero markers rather than becoming `Unsupported`.

## Accepted UI reference boundary

The supplied Grounded UI export is accepted as evidence for authored widget hierarchy, configured layout values, font roles, texture references and dimensions, named control states, the default global-color theme, and the alpha/value silhouettes visible in the narrowly selected settings PNGs. The settings textures are predominantly monochrome masks, so their white/gray pixels are not accepted as final runtime colors. The export is not accepted as proof of final runtime pixels after Unreal material processing, tinting, animation, scaling, localization, or platform-specific rendering.

Project UI work may adapt the settings language through semantic WPF resources, generous labeled rows, warm cream/orange/red hierarchy, condensed short headings, clipped-corner and angled-edge vector geometry, tapered switches, and explicit hover/focus/selected/disabled states. It must not weaken the save-analysis truth model: `Validated`, `Unknown`, `Unsupported`, and collection states remain explicit and may not be inferred or communicated by theme color alone. Extracted game fonts and textures are reference-only unless redistribution rights are established. Detailed implementation guidance is in `docs/UI_DESIGN_GUIDELINES.md`.

## Accepted calibration

Reference map: 1258 × 1258.

`pixelX = 629.335 + 0.00625103 × worldY`

`pixelY = 620.711 - 0.00624265 × worldX`

Store normalized coordinates; Z is metadata only.

## Historical exported map texture comparison

FModel's `UI_MapPanel.json` exports `TextureSize` 4096 × 4096, `MinBounds` (-100000, -100000), `MaxBounds` (100000, 100000), zoom levels 1.75/1.33/1.0/0.75/0.5, and current/default index 2 (1.0). The composed `T_UI_Worldmap` export is also 4096 × 4096.

The bounds suggest the candidate transform `x=(worldY+100000)*4096/200000`, `y=(100000-worldX)*4096/200000`. However, placement belongs to native `/Script/Maine MapPanelWidget`, and the exported Blueprint does not reveal that conversion. At the independently validated logout molar (-25383.043, -7996.126), the candidate is (1884.239, 2567.845), while the accepted screenshot calibration scaled to 4096 is (1886.345, 2536.943): a 30.974-pixel separation, almost entirely vertical. Therefore the candidate remains tentative and does not replace the accepted calibration.

## Exported bounds accepted for map placement (2026-09-12)

The user's oak-lab molar screenshot (guide #22) supplies an independent local placement check. It is a zoomed and panned view at the displayed 1.75× setting, not a full-map image. Registration uses the matching stepped oak outline in the native 512 × 512 background, never the screenshot edges: its top horizontal run occupies native columns 310–315 at row 230. In the 2048 × 1152 displayed screenshot that run starts at approximately (841, 665), and each native pixel spans about 14.9 screenshot pixels. The molar silhouette center at approximately (936, 700) therefore maps to about (316.4, 232.3), with a conservative 0.3-native-pixel measurement tolerance. Terrain is used only to register the viewport, not to infer a spawn or collection state.

A validated local save's selected oak-area record supplies the atomic world transform (9254.27734375, 23622.41015625, 3957.68359375). The historical screenshot calibration projects it to (316.235, 229.114); the exported bounds project it to (316.473, 232.309), agreeing with the independent image measurement. The old calibration is about 3.2 native pixels too high here. Guide #22 identifies the comparison area only; its approximate guide coordinates do not determine rendered position. The screenshot's collected icon is a visual position reference only and does not override the save's state.

Normal rendering now uses `u=(worldY+100000)/200000`, `v=(100000-worldX)/200000`, scaled to the map texture and then the viewport. The historical `WorldToReferenceMap` method remains available for diagnostics only. The native game's implementation is still not decompiled; this accepts its authored bounds using one independent local screenshot check, not a claim of exhaustive whole-map validation. Additional distant anchors remain useful. A regression first failed at V×512=229.114, then passed against the independently measured (316.4, 232.3) after the change.

The interactive save picker uses this bounds projection to place marker elements over the native 512 × 512 map. Each marker center is projected directly from the selected spawn's serialized X/Y transform. Picking a save triggers analysis and rendering immediately, and the persisted last-selected save is reloaded at startup. Unsupported or partial analyses clear the image rather than retaining or inventing markers.

The desktop application is intentionally limited to save picking, recursive folder monitoring, image preview, and manual refresh. Game-window tracking, transparent rendering, viewport calibration, click-through controls, and global hotkeys were removed; they do not participate in save analysis or preview rendering.

Grounded's `SaveGameScreenshot.jpg` was verified as a 512 × 512, 96-DPI image even though the in-game save card displays its image content through an approximately 11:6 aperture. The desktop current-save card therefore uses the same 11:6 inner aperture and expands the square encoding to that aperture. Treating the JPEG's nominal square dimensions as the scene's pixel aspect leaves the captured scene visibly compressed horizontally.

Folder monitoring is an explicit persisted user choice. When disabled, no `FileSystemWatcher` is active and the selected `World.csav` is refreshed only on request. The current-save card displays filesystem metadata and validated analysis counts only; it does not infer in-game world metadata from filenames, screenshots, or UI assets.

The desktop preview loads the validated native 512 × 512 logical map and keeps it separate from the authoritative marker overlay. This replaces the prior 4096 × 4096 upsample and 8× center-sampled reduction, which introduced small interpolation artifacts. Zoom ranges from the scale that fits the complete map in the viewport through 16× logical-pixel scale. Fit scale may enlarge the logical map and is determined by the limiting viewport axis; the map is centered on the other axis. The base map uses nearest-neighbor sampling so one logical retro pixel remains one solid displayed block. Marker elements remain 32 device-independent pixels at every zoom; only each marker center moves according to its normalized projected texture coordinate, preserving its authoritative image-relative position. A zoomed map can be panned by dragging it with the left mouse button.

On the four authoritative uncollected markers in the before-transition fixture, the candidate differs from the accepted calibration by 23.11–36.75 texture pixels. A 4096 × 4096 diagnostic render using the accepted calibration contained exactly four marker components centered at the expected projected coordinates.

## Accepted marker-free map reconstruction

The supplied in-game reference contains a 1024 × 1024 map inside a three-pixel top/left UI border. Its geometry is reproduced by three `UI_MapBGBackyard` textures:

- `T_UI_Worldmap_BG.png` alpha
- `T_UI_Worldmap_Base.png` luminance
- `T_UI_Worldmap_Water.png` alpha

FModel exports these layers 90° clockwise relative to the in-game map, so composition rotates them 90° counter-clockwise. A per-channel linear combination fitted to the in-game reference reproduces the runtime global-color tint. `T_UI_Worldmap_HazeOn` is not visible in this reference and is not required for the marker-free base map.

A 4096 × 4096 diagnostic reconstruction reduced to the reference's 1024 × 1024 inner area produced RGB RMSE 0.91, mean absolute error 0.46, and 99th-percentile absolute error 3 on a 0–255 channel scale. The production asset was then composed directly at the source layers' native 512 × 512 resolution to avoid interpolation artifacts. Only that validated result is retained and packaged.

## Accepted Grounded `.csav` container

Verified against the supplied `World.csav` and `World_decompressed.bin` on 2026-08-23:

- Bytes 0–3: little-endian `uint32` decoded size.
- Bytes 4–7: little-endian `uint32` compressed payload size.
- Bytes 8 onward: one Oodle/Kraken-family payload.
- Declared compressed size equals the remaining file length.
- Decoding the 1,270,025-byte payload produces exactly 5,336,053 bytes.
- The C# decoder output is byte-for-byte identical to the supplied Python-produced fixture.

`ooz v7.0` expects a different wrapper (`uint64` decoded size followed by the compressed bytes), so `OozKrakenDecoder` creates that wrapper only inside a disposable temporary directory. The executable is pinned by SHA-256.

## Not yet accepted

- Spawn-manager binary record layout and byte ordering.
- Platform save discovery.

These remain unsupported rather than heuristic.

## Accepted NG+ selected-spawn record profile v1

Validated against the independent logout fixture and the six-second before/after collection pair:

- Both randomized groups are serialized in `World.csav` with their actual entry counts.
- The counts vary by world/save: the logout fixture contains 56 normal + 10 underwater; the transition fixture contains 52 normal + 10 underwater. Counts are never hard-coded.
- Every entry contains one ASCII spawn-data asset followed by a 40-byte `FTransform` (quaternion, translation X/Y/Z, scale), a 16-byte spawn FGuid, fixed structural fields, and a 16-byte persistent actor FGuid.
- Unreal FGuid identity is four serialized little-endian `uint32` words and is displayed as `AAAAAAAA-BBBBBBBB-CCCCCCCC-DDDDDDDD`.
- The actor FGuid recurs in the persistent actor section when an actor-state record exists.

## Accepted collection and approach-state rule v1

The mixed-state logout save establishes that the persistent actor byte is an approach flag, not a collection flag: its 53 selected spawns contain 51 state-`0` actors not approached by the player and two state-`1` actors approached but not collected. Both states retain authoritative selected-spawn transforms and are uncollected.

Before fixture (`284d 22m 45s`): selected 62, collected 0, uncollected 62 (58 unapproached, 4 approached), unknown 0.

After fixture (`284d 22m 51s`): selected 62, collected 1, uncollected 61 (58 unapproached, 3 approached), unknown 0.

The transitioned selection is:

- Spawn: `DA9E1DE9-42705554-F73E408B-7E657919`
- Actor: `01C60192-436E4C33-92BFFDB9-B27E65EE`
- World: `(-35821.598, 70946.258, 2342.250)`

Its persistent state was `1` before collection and its persistent actor record was absent afterward. The three other state-`1` selected actors remained unchanged. Therefore state `0` means unapproached, state `1` means approached, and an absent persistent actor record means collected. Collection and approach are represented separately in the model.

Independent logout regression: selected 66, collected 0, uncollected 66 (65 unapproached, 1 approached). The approached spawn is `A7683985-40FC4A4E-AB8B6786-B15C7B9C` at `(-25383.043, -7996.126, 1306.095)`.

Normal map rendering includes every authoritative uncollected selection. Unapproached markers default to 45% opacity and can be adjusted from 0–100% with the persisted desktop slider; approached markers remain at full opacity.

## Accepted bounded refresh implementation

Recursive monitored-save discovery runs as cancellable worker work and selects the latest `World.csav` in one pass. Ordering remains newest `LastWriteTimeUtc` first with case-insensitive full-path ordering as the tie-break, matching the prior observable selection without sorting and retaining every candidate. A newer watcher event cancels in-flight discovery or analysis and reuses the existing one-second debounce.

The desktop loads its validated native 512 × 512 map on a worker and passes only the frozen bitmap to the WPF dispatcher. It no longer decodes, allocates, or reduces a 4096 × 4096 image at runtime. Marker popup trees and optional guide lookups are created only when their marker is first opened, while marker positions, hit targets, popup text, and preference behavior remain unchanged. Settings writes are suppressed when the complete persisted value is unchanged; selected-path, folder-monitoring, opacity, and guide-hint changes still persist synchronously at their existing interaction points.

The v1 analyzer now validates and returns selected spawns and persistent actor records as one atomic profile operation. Persistent actor GUID occurrences are located in one complete decoded-save pass with a full-width GUID dictionary lookup at each byte offset, rather than comparing every same-prefix selected actor. This is linear in save length and cannot degrade to `save bytes × same-prefix actors`. The scan checks cancellation every 4096 offsets. The same reference-count and persistent-signature rules remain fail-closed. Deterministic synthetic regressions cover recognized state `1`, an absent persistent record, an unrecognized second record, more than two GUID occurrences, exactly one actor lookup pass per analysis, 1024 same-prefix GUIDs over 8 MiB, and cancellation.

The desktop process lazily initializes and reuses its decoder/analyzer service, so the pinned `ooz.exe` integrity initialization is not repeated on refresh or retry. The decoder retains the hash-verified executable bytes and launches a private copy staged from that snapshot in its random per-decode working directory. It re-hashes the staged file through a read-only handle that denies writes, deletion, and replacement, and holds that handle until `Process.Start` returns with the executable image loaded. Replacing either the original executable after initialization or the staged executable during launch therefore cannot change the program launched through ordinary filesystem operations. Parse results and decoded save bytes are not retained by that service. The validated 512 × 512 base-map bitmap and marker icon are also lazily decoded and frozen once per process; confidence and marker-state guards still run before every base-map access.

Self-contained WPF releases have a security floor of Microsoft Windows Desktop Runtime 10.0.13. `global.json` selects SDK 10.0.303, the app pins `RuntimeFrameworkVersion` 10.0.13, and packaging is intentionally multi-file so the release gate can inspect the product versions of the actual published WPF assemblies before creating a ZIP. A weekly advisory query covers the implicit `Microsoft.WindowsDesktop.App.Runtime.win-x64` package, which ordinary project package checks do not reliably cover.

The validated decoded World fixture is 5.34 MiB. Until larger authoritative fixtures prove a need, `.csav` input is limited to 32 MiB compressed and 64 MiB decoded (roughly 6× and 12× the validated decoded fixture respectively). The physical file length and eight-byte header are checked from a stream before payload allocation or Kraken launch. The broker pre-creates one private output file, grants the AppContainer write access only to that file, enforces the private-directory quota while the helper runs, verifies the exact output length before allocation, and reads exactly that length.

`ooz.exe` executes with a fresh AppContainer identity and no capabilities, so Windows denies network and filesystem writes except to the single broker-created output file. The AppContainer receives read/execute access only to the private staging directory containing the hash-verified executable and bounded compressed input. Process creation supplies only stdin/stdout/stderr through an inherited-handle allowlist, assigns the suspended helper to a kill-on-close Job before its first instruction, and limits active processes to one, memory to 256 MiB, CPU to 15 seconds, wall time to 60 seconds, private-directory bytes, and diagnostics to 16 KiB per stream. File output is required because `ooz -c` uses Windows text-mode stdout and inserts carriage returns before decoded line-feed bytes, so it is not byte preserving. Limit, launch, crash, exit, oversized output, or malformed-output failures become `Unsupported`. Production-path integration probes cover unauthorized private/outside write denial, the one authorized output, loopback networking, child creation, memory, CPU, timeout, diagnostic capture, and output bounds.

Windows can transiently return `ERROR_FILE_NOT_FOUND` while a newly created AppContainer profile becomes launch-ready. The broker retries only that specific process-creation error against the same profile for at most five seconds, checking cancellation between attempts. Every other launch error still fails immediately, and the helper remains suspended until it is assigned to the Job.

Authoritative save-adjacent screenshots are exactly 512 × 512. Automatic preview accepts only bounded PNG/JPEG files up to 4 MiB whose header parser proves the signature, one frame, exact dimensions, complete encoded boundary, and no trailing image. PNG IHDR validation also enforces the specification's permitted color-type and bit-depth combinations before WPF/WIC sees the bytes. WPF decode is performed from the validated byte snapshot on a worker thread with 512-pixel decode bounds and cancellation; rejection remains independent from save analysis and marker rendering.

## Accepted discovered-POI record profile v1

Grounded serializes the party's authoritative discovery rows inside the single `/Script/Maine.PartyComponent` record in `World.csav`. The component has a bounded `uint32` payload. Its first field is a zero version byte followed by a contiguous sequence of `FDataTableRowHandle` values whose table is exactly `/Game/Blueprints/Items/Table_AllItems.Table_AllItems`; the sequence boundary is followed by a bounded count and tag `1`. Rows beginning with `POI` are the discovered map-location identifiers. The decoder validates the component count, payload boundary, exact data-table signature, ANSI `FString` boundaries, POI identifier characters, sequence-boundary signature, and uniqueness. Any mismatch returns `Unsupported` with no discovered locations.

Chronological validation on one early world:

- Game time `2d, 7m, 12s`, Oak Hill: 8 discovered POI rows.
- Game time `5d, 13m, 12s`, Cave: 15 discovered POI rows.
- The later save contains every earlier row plus exactly `POICalvoCan`, `POIFourLeafClover`, `POIGrasslandsFieldStationRoots`, `POIGrasslandsJabby`, `POIGrasslandsLemonCrime`, `POIGrasslandsWelp`, and `POIMilkCarton`.
- Four subsequent autosave/logout records remain at the same 15 POIs.

Independent validation covered 46 saves from six 2026 worlds. Every record matched structurally. Observed progressions included `0`, `8 → 15`, `44 → 46 → 59 → 70 → 76 → 77 → 79`, and an NG+ transition from 105 pre-remix POI identifiers to 102 post-remix identifiers. The remix boundary is a world-state transition and is not treated as ordinary monotonic exploration.

This profile proves named discovered POIs. It does not prove that Grounded stores a continuous fog-of-war bitmap or arbitrary walked-area coverage, and the tool must not synthesize such coverage from POI positions.

## Community guide hints remain presentation-only

The desktop marker popup may enrich an already authoritative selected spawn with the nearest entry from the Grounded Wiki's 219-location NG+ interactive guide. The editable source snapshot is `src/GroundedMolar.App/Data/milk-molar-guide.json`. Each guide coordinate comes directly from the interactive map marker's `position: [mapY, mapX]` field: the catalog stores `guideX = position[1]` and `guideY = position[0]` against the source map's 1254 × 1254 top-left-origin image. The guide source texture has the same unrotated orientation as the exported UI layers, while MolarMap's accepted map reconstruction rotates those layers 90° counter-clockwise. Sandbox, Hedge, picnic-table, Undershed, and wheelbarrow guide pins all independently land on their named regions only after that rotation. Matching therefore compares each pin at `orientedX = guideY`, `orientedY = 1254 - guideX` after converting the authoritative spawn through the exported UI bounds. It chooses the nearest entry and separates the guide's 21 underwater entries from its 198 normal entries. Guide #113 is the explicit sandbox regression for this orientation correction, and all 219 pins round-trip through the oriented lookup. Repeated prose is retained because distinct source pins can carry identical descriptions.

Guide number, category, coordinates, and location prose are community-authored, approximate, and optional; they do not participate in spawn reconstruction, persistent actor resolution, confidence, the accepted marker projection, or normal marker eligibility. One persisted global preference controls guide number, hint text, and the approximation warning across every marker popup; the warning is rendered only while hints are enabled. The popup contains no per-marker hint toggle. The preferences card links to the source guide through a confirmation dialog that displays the full external URL before offering Open and Cancel. If the guide catalog cannot load or validate, the application must fail the hint independently rather than changing validated save state.

## Reviewed guide identity corrections (2026-09-12)

The user reported the rake/cave descriptions swapped and the inside-pot/rim hints duplicated. Reading selected records from 25 recent local saves reproduced the problem: the rake spawn at Z=5932.927 chose cave entry 194, the cave spawn at Z=1692.181 chose rake entry 195, and both pot spawns (Z=2412.161 inside, Z=4492.684 rim) chose entry 114. The user supplied the intended number/description associations. The source snapshot also had the two pot numbers reversed. Approximate X/Y matching alone cannot resolve these cases.

Optional `matchedSpawnGuid` values in `milk-molar-guide.json` now bind entries 194/195 to cave/rake and 114/115 to inside-pot/rim respectively. Pot numbers are corrected to 14 inside and 15 rim. Original guide coordinates and prose remain in the file. Runtime code contains no per-molar descriptions, numbers, GUIDs, or height thresholds: it first looks for a matching serialized spawn GUID, checks underwater compatibility, and otherwise retains the existing approximate coordinate lookup. GUID metadata must use four eight-hex-digit words and be unique case-insensitively; malformed or ambiguous metadata rejects the catalog independently of save analysis. A matched GUID with an incompatible underwater category returns no hint.

These bindings are optional hint enrichment only, not a spawn list or state evidence. Matching operates on one already parsed spawn and never subtracts candidates or depends on which others are selected/collected. Tests cover all four authoritative transforms with both collected and uncollected states, invalid/duplicate metadata, and edited descriptions loading from the file. Other unreviewed guide matches remain approximate. No private save fixtures are committed.

## Trusted dual-channel release uses synchronized channel-native packages

Version 1.0.1 uses an x64 MSIX uploaded directly to Microsoft Store and a separately signed self-contained offline x64 MSI inside the Nexus/direct-download ZIP. Both packages must come from the same source revision, product version, runtime floor, helper source bytes, and user-visible release notes. The Store package uses the Partner Center identity `Gweko.MolarMap` and publisher `CN=7F7844FD-9C33-43BF-91A4-548741E63E59`; it is intentionally unsigned before submission because Microsoft applies the trusted Store signature after certification. It must never be distributed through Nexus or direct download.

The Nexus/direct pipeline signs a staged `ooz.exe` before compilation because Authenticode changes the helper bytes, compiles that exact signed SHA-256 into `GroundedMolar.Core.dll`, re-runs regression checks against the signed helper, signs only unsigned project payloads, rejects broken pre-existing signatures, verifies every PE, builds and signs the MSI, and records artifact hashes in a release manifest. Unsigned rehearsal output is named `UNSIGNED-DO-NOT-PUBLISH` and marked `productionReady: false`.

When both channel manifests exist, either packaging pipeline runs a cross-channel gate that requires matching product, version, architecture, and helper source hash, validates the Store-only readiness flags, and proves that the Nexus ZIP contains no MSIX.

The public signing identity authenticates the redistributed helper bytes but does not claim authorship of upstream `ooz`. The installed app and MSI retain the original single-molar `MolarMap.ico` with no map background. The separate marketplace-only artwork is an AI-generated design with three consistent molar POI symbols on an abstract cropped backyard-map fragment and explicit provenance disclosure; it avoids both a full game-map reproduction and navigation imagery, and does not use requested Grounded, Obsidian, Xbox, or Microsoft logos.

## Mixed NG+ molar asset within the general group (2026-09-12)

The reported Oak Hill save (game time 0d, 9m, 40s) decodes to 2,724,292 bytes and contains 56 general-group selections plus 10 underwater-group selections. General-group entry 30 carries the exact known underwater NG+ spawn-data asset. Its FString, transform, spawn GUID, actor GUID, structural fields, and persistent actor signature match the accepted layout. Requiring the normal asset for every general-group entry incorrectly rejected this save.

The general group now accepts either of the two proven NG+ molar spawn-data assets; the dedicated underwater group still accepts only its proven underwater asset. IsUnderwater derives from the serialized spawn-data asset, while SpawnGroup preserves the original group. All other layout, uniqueness, actor-state, and confidence checks remain unchanged. The reported save validates with 66 selected/uncollected, zero collected/unknown, and 11 underwater assets in total. Synthetic regression covers the mixed group, atomic identity/transform, actor state, map loading, and unknown-asset rejection. An optional GROUNDED_MIXED_GROUP_SAVE regression decodes the private save through the production decoder and loads its frozen 512 x 512 WPF map. Private data stays in ignored artifacts only.

## Desktop error logging (2026-09-12)

Errors append UTC timestamps, context, rejection diagnostics, and exception details to logs/MolarMap.log beside the executable. Logs rotate at 2 MiB with one previous file. If the install folder is protected, logging falls back to LocalAppData/MolarMap/logs. Logging failures cannot crash the app. Save rejection, refresh/retry, screenshot, guide, watcher, preference, and unhandled UI/process/task errors are covered; expected cancellation is excluded. Logs may contain local paths and diagnostic identifiers, but never decoded save payloads. Regression verifies append, rotation, and an unwritable destination.

## User-reviewed wiki pair bindings (2026-09-12)

The user resolved the four remaining audited collisions using directions explicitly confirmed to refer to the wiki guide map: #73 left/#72 right, #109 left/#108 right, #118 left/#116 right, and #218 above/#219 below. Atomic GUID/XYZ observations from the existing validated-save audit identify the pair members in the documented guide coordinate frame. The eight bindings and evidence are listed in MOLAR_GUIDE_AUDIT.md and serialized as matchedSpawnGuid in the JSON. Existing descriptions already match the supplied identities. Source pin coordinates remain a transcription; they do not replace save transforms. These are hint identity corrections only and do not establish selected spawns, collection state, or format confidence. Regression checks resolve all eight independently across collected, uncollected, and unknown states.

### Brawny Bin binding correction (2026-09-12)

The user reported that the initial #218/#219 bindings were reversed. Corrected #218 (between the game box and cardboard box) to 9B5FFE39-496231A0-8AB36BB2-43255BB4 and #219 (inside the blue waffle ball) to CD10B7F4-43680B03-C33215A6-F38058D6. This direct review supersedes the previous interpretation of wiki above/below. JSON bindings, regression expectations, and the audit table now agree; descriptions and source coordinates are unchanged.

## 2026-09-12 — Portable 1.0.2 distribution

Preserve the existing unsigned, self-contained Windows x64 portable ZIP for GitHub and Nexus at the user's direction. The portable script is separate from signed MSI and Store packaging. Include versioned release notes and SHA-256 checksums; never include private save fixtures.

## Windows Desktop runtime security update (2026-10-24)

The live GitHub Advisory Database query for `Microsoft.WindowsDesktop.App.Runtime.win-x64@10.0.11` returned 11 advisories, including [CVE-2026-70035](https://github.com/dotnet/wpf/security/advisories/GHSA-4j9v-h7pp-725h). Every returned .NET 10 affected range ends at 10.0.12 and identifies 10.0.13 as the first patched version. Microsoft's [.NET 10 release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json) confirms Windows Desktop 10.0.13 was released on 2026-10-13.

The app runtime pin, default advisory query, and actual published-WPF-assembly release floor are now 10.0.13. SDK 10.0.303 remains the build-tool baseline; self-contained publication explicitly selects the patched runtime rather than relying on the SDK's bundled version. Existing distributed self-contained packages must be rebuilt to receive the fix. No save profile, collection-state, projection, or UI behavior changes.

The repository NuGet configuration enables NuGet.org for normal dependency restore. Clearing all feeds made a normal build fail with NU1100 for the pinned 10.0.13 app-host pack unless its active package cache had been populated separately. Build tooling may use the network; the offline requirement applies to the application. Verification retains workspace-isolated .NET and NuGet state.
