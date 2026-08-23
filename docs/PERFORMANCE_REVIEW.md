# Application performance review

## Scope and constraints

This review targets refresh latency, allocation pressure, and UI responsiveness without changing visible behavior, controls, marker placement, save-format decisions, or fail-closed semantics. Private save fixtures must remain outside the repository.

## Findings and requested improvements

### P1 — Cache immutable WPF image assets

`SaveMapImageRenderer.LoadMap` currently opens and decodes the 4096 × 4096 PNG, converts it to BGRA32, allocates a roughly 64 MiB source buffer, and downsamples it to 512 × 512 on every validated refresh. `LoadMarkerIcon` also decodes the same immutable icon on every marker rebuild.

Requested change:

- Lazily load, validate, reduce, and freeze the base map once per process.
- Lazily load and freeze the marker icon once per process.
- Keep the existing confidence/state guards on every `LoadMap` call before returning the cached map.
- Preserve dimensions, nearest-neighbor display behavior, and all error behavior relevant to unsupported analyses.

Acceptance checks:

- Repeated valid calls return the same frozen bitmap instances.
- Invalid analysis still fails before an image is returned.
- No UI or XAML changes.

### P1 — Remove repeated whole-save scans in the v1 profile

The analyzer calls `CanParse`, `ReadActors`, and `ReadMolarSpawns`. Today `CanParse` itself calls both readers, while `ReadActors` calls `ReadMolarSpawns` again. In addition, `ReadActors` performs `FindAll` across the complete decoded save separately for each selected actor GUID. For a typical 5 MiB save with roughly 60–70 selected spawns, this creates hundreds of MiB of repeated memory inspection per parsing pass, multiplied by redundant passes.

Requested change:

- Preserve the public interfaces and exact validation semantics.
- Within a single profile operation, avoid reparsing selected spawns unnecessarily.
- Replace the per-actor whole-save searches with one evidence-equivalent pass that locates occurrences of the selected actor GUIDs, or another bounded approach demonstrably faster on representative synthetic data.
- Continue rejecting duplicate GUIDs, unsupported reference counts, multiple persistent-state records, missing/unrecognized second records, truncated signatures, and changed record layouts exactly as before.
- Do not infer collection state or weaken `Unsupported` behavior.

Acceptance checks:

- Add regression checks covering occurrence counts and recognized/unrecognized persistent-state signatures before changing the parser.
- Add a deterministic synthetic performance regression check or instrumentation proving the actor lookup no longer performs one full-save search per selected actor. Avoid flaky wall-clock thresholds where possible.
- Existing tests and fixture-gated tests remain valid.

### P2 — Reuse stable analysis services

`AnalyzeWithRetryAsync` constructs the decoder, hashes `ooz.exe`, and constructs the analyzer/profile on every attempt and every refresh. These services are immutable for the application lifetime.

Requested change:

- Reuse a lazily initialized decoder/analyzer or a single analysis service across refreshes.
- Preserve background execution, retry timing, exception handling, and the pinned executable-integrity check.
- Ensure initialization failures still lead to the same user-facing failure state.

Acceptance checks:

- A regression seam demonstrates repeated analyses reuse initialized services without sharing mutable parse state.
- No watcher, debounce, retry, or presentation behavior changes.

## Out of scope

- Changing debounce duration, retry delays, zoom/pan behavior, marker opacity, copy, layout, or any visible UX.
- Parallelizing format parsing in a way that complicates deterministic validation.
- Memory-mapping private saves, retaining decoded saves, weakening integrity checks, or adding heuristic parsing.
- Packaging and platform save discovery.

## Verification required

Run:

```text
dotnet build
dotnet run --project tests/GroundedMolar.Tests
```

Record any proven implementation-level format/performance decision in `docs/DECISIONS.md`. Report any fixture checks that were skipped because their environment variables were unavailable.
