# Third-party components

## ooz v7.0

GroundedMolar currently invokes `ooz.exe` as a separate process to decode trusted local Oodle/Kraken payloads. The included binary is pinned to SHA-256 `271D3FD02E582175FF033D0A23DCA3785B6888FA21B8CD06741BA8C19B71DF41`.

Upstream project: <https://github.com/powzix/ooz>

GroundedMolar redistributes the unmodified Windows binary from upstream release v7.0 (commit `0503806`). The upstream source headers license ooz under GNU GPL version 3 or later. The release archive therefore includes the GPL text and this notice; corresponding source is available from the tagged upstream source at <https://github.com/powzix/ooz/tree/v7.0>. GroundedMolar invokes it as a separate program and does not incorporate its source.

The decoder warns that it is not fuzz-safe, so GroundedMolar validates container sizes and uses it only for user-selected local Grounded save data.

## Grounded visual material

The map and Milk Molar marker in the app are Grounded-derived fan-project material. Grounded and related marks and assets belong to their respective owners. They are included only for this free, unofficial Grounded companion utility and are not licensed for reuse outside that context.
