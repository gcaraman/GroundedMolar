# Roadmap

## Completed — authoritative molar analysis

- Exact selected-spawn boundaries for both NG+ molar groups.
- Atomic FGuid, actor FGuid, transform, group, spawn data, and index decoding.
- Persistent actor state validated against before/after collection saves.
- Structural profile detection and fail-closed confidence.
- CLI output of authoritative uncollected GUIDs and coordinates.
- Fail-closed decoding and CLI output of authoritative party-discovered POI identifiers.

Completed foundation: `.csav` framing, Kraken backend invocation, output-length validation, CLI integration, temporary-file cleanup, and byte-for-byte fixture regression.

## Next — save discovery and desktop viewer

- Add automatic Steam/Xbox save discovery. Direct save/folder picking, recursive latest-save monitoring, native-resolution map-image display, and debounced refresh are implemented.
- Add normalized projection to CLI/debug output.
- Validate the native map-widget transform with independent in-game anchors.

Completed preparation: accepted projection scaled to the exported 4096 × 4096 map, explicit comparison with the tentative game-bounds transform, and a fail-closed standalone 4096 × 4096 PNG preview renderer.

Completed map reconstruction: marker-free game map rebuilt from three exported UI layers, orientation corrected, and compared pixel-for-pixel with the supplied in-game reference.

Current validation gate: compare either projection against one visible in-game marker at default zoom and centered/full-map pan. The four before-save markers show a 23.11–36.75 texture-pixel separation between transforms.

## Completed — desktop save-map viewer

- Direct save picking and recursive latest-save folder monitoring implemented.
- Native 4096 × 4096 fail-closed image preview and manual refresh implemented.
- Logical-pixel-preserving fit-to-window through 16× zoom and mouse-drag panning implemented with fixed-size, center-anchored molar markers.
- Persisted save/folder selection, debouncing, and transient-read retries implemented.

## Next — validation and packaging

- Validate behavior while Grounded rewrites `World.csav` during play.
- Add automatic Steam/Xbox save discovery.
- Self-contained Windows x64 packaging is implemented; complete the manual Nexus upload checks in `docs/NEXUS_RELEASE.md` before publishing.
