# GroundedMolar agent guide

## Mission

Build an offline Windows tool that derives unfound NG+ Milk Molar markers from authoritative Grounded save records. Never infer a spawn or state from landmarks, icon colors, proximity, or candidate-list subtraction.

## Non-negotiable invariants

- Reconstruct selected spawns before resolving collection state.
- Use spawn GUIDs as identity and parse GUID plus X/Y/Z atomically.
- Unproven state is `Unknown`; unproven formats are `Unsupported`.
- Normal rendering requires `Validated` confidence and `Uncollected` state.
- Keep decoding, parsing, state resolution, projection, and UI separate.
- Never commit private save fixtures.

## Working loop

1. Read `docs/BUILD_SPEC.md` and `docs/ROADMAP.md`.
2. Add a failing regression check for each understood binary structure.
3. Implement the smallest evidence-backed change.
4. Run `dotnet build` and `dotnet run --project tests/GroundedMolar.Tests`.
5. Record proven format decisions in `docs/DECISIONS.md`.

## Verification rule

- Always check your work after making changes. Run the relevant build, tests, static checks, and visual or behavioral verification appropriate to the change before reporting completion.

## Current boundary

Grounded World profile v1 is implemented and fixture-validated for selected NG+ molar records and persistent actor state. The WPF app is limited to save picking, recursive folder monitoring, image preview, and manual refresh. Any changed record signature must fail closed as `Unsupported` until a new profile is proven.
