# Evidence and decisions

## Accepted UI reference boundary

The supplied Grounded UI export is accepted as evidence for authored widget hierarchy, configured layout values, font roles, texture references and dimensions, named control states, the default global-color theme, and the alpha/value silhouettes visible in the narrowly selected settings PNGs. The settings textures are predominantly monochrome masks, so their white/gray pixels are not accepted as final runtime colors. The export is not accepted as proof of final runtime pixels after Unreal material processing, tinting, animation, scaling, localization, or platform-specific rendering.

Project UI work may adapt the settings language through semantic WPF resources, generous labeled rows, warm cream/orange/red hierarchy, condensed short headings, clipped-corner and angled-edge vector geometry, tapered switches, and explicit hover/focus/selected/disabled states. It must not weaken the save-analysis truth model: `Validated`, `Unknown`, `Unsupported`, and collection states remain explicit and may not be inferred or communicated by theme color alone. Extracted game fonts and textures are reference-only unless redistribution rights are established. Detailed implementation guidance is in `docs/UI_DESIGN_GUIDELINES.md`.

## Accepted calibration

Reference map: 1258 × 1258.

`pixelX = 629.335 + 0.00625103 × worldY`

`pixelY = 620.711 - 0.00624265 × worldX`

Store normalized coordinates; Z is metadata only.

## Exported map texture comparison — not a replacement calibration

FModel's `UI_MapPanel.json` exports `TextureSize` 4096 × 4096, `MinBounds` (-100000, -100000), `MaxBounds` (100000, 100000), zoom levels 1.75/1.33/1.0/0.75/0.5, and current/default index 2 (1.0). The composed `T_UI_Worldmap` export is also 4096 × 4096.

The bounds suggest the candidate transform `x=(worldY+100000)*4096/200000`, `y=(100000-worldX)*4096/200000`. However, placement belongs to native `/Script/Maine MapPanelWidget`, and the exported Blueprint does not reveal that conversion. At the independently validated logout molar (-25383.043, -7996.126), the candidate is (1884.239, 2567.845), while the accepted screenshot calibration scaled to 4096 is (1886.345, 2536.943): a 30.974-pixel separation, almost entirely vertical. Therefore the candidate remains tentative and does not replace the accepted calibration.

The standalone preview scales accepted normalized coordinates to the 4096 × 4096 texture. Normal save rendering remains fail-closed: only `Validated` analysis and its `Uncollected` records are rendered.

The interactive save picker uses the same accepted projection to create a native 4096 × 4096 in-memory map image. Each marker center is projected directly from the selected spawn's serialized X/Y transform; display resizing occurs only after the complete bitmap is rendered. Picking a save triggers analysis and rendering immediately, and the persisted last-selected save is reloaded at startup. Unsupported or partial analyses clear the image rather than retaining or inventing markers.

The desktop application is intentionally limited to save picking, recursive folder monitoring, image preview, and manual refresh. Game-window tracking, transparent rendering, viewport calibration, click-through controls, and global hotkeys were removed; they do not participate in save analysis or preview rendering.

Grounded's `SaveGameScreenshot.jpg` was verified as a 512 × 512, 96-DPI image even though the in-game save card displays its image content through an approximately 11:6 aperture. The desktop current-save card therefore uses the same 11:6 inner aperture and expands the square encoding to that aperture. Treating the JPEG's nominal square dimensions as the scene's pixel aspect leaves the captured scene visibly compressed horizontally.

Folder monitoring is an explicit persisted user choice. When disabled, no `FileSystemWatcher` is active and the selected `World.csav` is refreshed only on request. The current-save card displays filesystem metadata and validated analysis counts only; it does not infer in-game world metadata from filenames, screenshots, or UI assets.

The desktop preview reduces the validated 4096 × 4096 reconstruction to its 512 × 512 logical pixel grid, then keeps that map and its authoritative marker overlay as separate layers. The 8× reduction was visually confirmed at maximum zoom: the intermediate 1024 grid still represented each real pixel with a 2 × 2 block. Zoom ranges from the scale that fits the complete map in the viewport through 16× logical-pixel scale. Fit scale may enlarge the logical map and is determined by the limiting viewport axis; the map is centered on the other axis. The base map uses nearest-neighbor sampling so one logical retro pixel remains one solid displayed block. Marker elements remain 32 device-independent pixels at every zoom; only each marker center moves according to its normalized projected texture coordinate, preserving its authoritative image-relative position. A zoomed map can be panned by dragging it with the left mouse button.

On the four authoritative uncollected markers in the before-transition fixture, the candidate differs from the accepted calibration by 23.11–36.75 texture pixels. The diagnostic `preview/before-save-projection-comparison.svg` embeds the exported map and displays both positions for each marker. The primary `preview/before-save-uncollected-molars.png` is a true 4096 × 4096 render using the accepted calibration; pixel inspection found exactly four orange marker components centered at the expected projected coordinates.

## Accepted marker-free map reconstruction

The supplied in-game reference contains a 1024 × 1024 map inside a three-pixel top/left UI border. Its geometry is reproduced by three `UI_MapBGBackyard` textures:

- `T_UI_Worldmap_BG.png` alpha
- `T_UI_Worldmap_Base.png` luminance
- `T_UI_Worldmap_Water.png` alpha

FModel exports these layers 90° clockwise relative to the in-game map, so composition rotates them 90° counter-clockwise. A per-channel linear combination fitted to the in-game reference reproduces the runtime global-color tint. `T_UI_Worldmap_HazeOn` is not visible in this reference and is not required for the marker-free base map.

The generated `preview/grounded-marker-free-map.png` is 4096 × 4096. After reducing it to the reference's 1024 × 1024 inner area, pixel comparison reports RGB RMSE 0.91, mean absolute error 0.46, and 99th-percentile absolute error 3 on a 0–255 channel scale. This accepts the three-layer reconstruction and its orientation for the standalone map asset.

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

The standalone preview has an explicit `--show-collected true` diagnostic layer. It preserves normal rendering defaults and draws only authoritatively resolved `Collected` records in pink (`#FF4EA8`) at their selected-spawn transforms. The `--collected-poc` diagnostic reproduces the single validated before/after transition above when private fixtures are unavailable; it does not introduce or infer any additional spawn.

## Accepted mixed spawn data within molar groups

Validated against a live autosave (`World.csav`, decoded 4,807,957 bytes) on 2026-08-24:

The `SG_NG+_MilkMolars` group contained 40 entries, of which one (index 8) carried `SD_MilkMolar_Underwater_NG+` spawn data instead of `SD_MilkMolar_NG+`. The `SG_NG+_MilkMolarsUnderwater` group contained 6 entries, all `SD_MilkMolar_Underwater_NG+`. The individual binary record layout (40-byte `FTransform`, 16-byte spawn `FGuid`, fixed structural fields, 16-byte actor `FGuid`) was byte-for-byte identical to the previously validated format.

`isUnderwater` is therefore derived from each entry's spawn-data asset path, not from the group it belongs to. Both `SD_MilkMolar_NG+` and `SD_MilkMolar_Underwater_NG+` are accepted in either group; any other spawn-data value still fails closed as `Unsupported`. The `SG_NG+_MilkMolars` and `SG_NG+_MilkMolarsUnderwater` group-asset markers still uniquely identify the two groups; their individual counts are still authoritative.

## Accepted empty underwater group

Validated against a live autosave on 2026-08-25 where the `SG_NG+_MilkMolarsUnderwater` group carried `count=0`. In that case the serialization ends immediately after the 4-byte count field: no reserved field and no entries follow. Reading the 4 bytes that would otherwise be the reserved field instead reads unrelated adjacent data. The count=0 path therefore returns an empty list without consuming any further bytes. The normal group is still required to have at least one entry; an absent or empty normal group still fails closed.

## Accepted bounded refresh implementation

The v1 analyzer now validates and returns selected spawns and persistent actor records as one atomic profile operation. Persistent actor GUID occurrences are located in one complete decoded-save pass with a full-width GUID dictionary lookup at each byte offset, rather than comparing every same-prefix selected actor. This is linear in save length and cannot degrade to `save bytes × same-prefix actors`. The scan checks cancellation every 4096 offsets. The same reference-count and persistent-signature rules remain fail-closed. Deterministic synthetic regressions cover recognized state `1`, an absent persistent record, an unrecognized second record, more than two GUID occurrences, exactly one actor lookup pass per analysis, 1024 same-prefix GUIDs over 8 MiB, and cancellation.

The desktop process lazily initializes and reuses its decoder/analyzer service, so the pinned `ooz.exe` integrity initialization is not repeated on refresh or retry. The decoder retains the hash-verified executable bytes and launches a private copy staged from that snapshot in its random per-decode working directory. It re-hashes the staged file through a read-only handle that denies writes, deletion, and replacement, and holds that handle until `Process.Start` returns with the executable image loaded. Replacing either the original executable after initialization or the staged executable during launch therefore cannot change the program launched through ordinary filesystem operations. Parse results and decoded save bytes are not retained by that service. The validated 512 × 512 base-map bitmap and marker icon are also lazily decoded and frozen once per process; confidence and marker-state guards still run before every base-map access.

Self-contained WPF releases have a security floor of Microsoft Windows Desktop Runtime 10.0.11. `global.json` selects SDK 10.0.303, the app pins `RuntimeFrameworkVersion` 10.0.11, and packaging is intentionally multi-file so the release gate can inspect the product versions of the actual published WPF assemblies before creating a ZIP. A weekly advisory query covers the implicit `Microsoft.WindowsDesktop.App.Runtime.win-x64` package, which ordinary project package checks do not reliably cover.

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
