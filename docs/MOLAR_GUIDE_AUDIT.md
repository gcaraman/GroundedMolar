# Milk Molar guide audit — 2026-09-12

The full 219-entry catalog was audited. Source transcription is consistent, but **most spawn-to-guide associations remain approximate**, not independently validated in-game. Marker positions and collection states continue to come exclusively from parsed save records.

## Source comparison

Compared with [Grounded Wiki map revision 50346](https://grounded.fandom.com/wiki/Map:Ng_plus_molar?oldid=50346), read from the public page's embedded map data:

- All 219 coordinate pairs and categories agree at the catalog's six-decimal precision.
- All descriptions agree after whitespace normalization; none were replaced with program constants.
- The source contains 219 marker records even though its introductory sentence says 218. No entry was removed based on that sentence.
- Source marker IDs have shifted in places. Local catalog IDs were retained; comparison used content and coordinates, not array order or a presumed source-ID identity.
- The two user-reviewed pot numbering corrections remain intentional differences from the source: inside is #14, rim is #15.
- The BBQ-wall source popup says #14, but its [attached image is named Milk Molar 140](https://static.wikia.nocookie.net/grounded9429/images/9/91/Milk_Molar_140.png/revision/latest?cb=20250425165743). The local number was corrected to #140 using that corroborating evidence, not merely the missing number in the sequence.
- The #73 source popup refers to an image named Milk Molar 72 and shares its description with #72. The user confirmed these are neighboring molars in the same location, with #73 left of #72 on the wiki map; the source image/title inconsistency does not collapse their identities.

Comparison fingerprints (FNV-1a over UTF-16 code units; diagnostic checksums, not security hashes): coordinate/category rows sorted by GuideX then GuideY = `3068442300`; normalized number/category/coordinate/description rows before intentional local number corrections = `1648109141`. Both local and live-source values matched. The source revision and audit note are recorded in the JSON.

## Corrected eastern laser pair

The newest save contained both reported spawns. Their parsed positions establish left/right placement; the user supplied the left marker's identity as #34. Entries retain the existing descriptions and guide coordinates and now carry explicit `matchedSpawnGuid` bindings:

| Guide | Spawn GUID | World X | World Y | World Z |
| --- | --- | ---: | ---: | ---: |
| 34 — middle section | 7E4EE2A8-42E257C8-97A374B2-4C0256D4 | -17066.912109375 | 18710.02734375 | 5623.3828125 |
| 35 — cable box | 624F5ABC-4DC959A0-B9243CBB-7A835281 | -17377.90625 | 19619.666015625 | 8087.1845703125 |

Both previously chose #35 by nearest X/Y. The corrected runtime lookup returns distinct numbers, independent of which other spawns are selected or collected. The catalog now covers numbers 1–219 exactly once, enforced by the loader.

## Original save coverage before the follow-up corrections

The final scan read 162 local save files: 106 validated, 46 unsupported, and 10 rejected by the decoder. Only validated analyses contributed observations. These contained 199 distinct selected spawn GUIDs across 14 world identifiers, with no conflicting XYZ/category for a repeated GUID. Saves were not changed; private observations and reports remain under ignored `artifacts/`.

| Catalog status | Entries |
| --- | ---: |
| Reviewed GUID binding observed | 6 |
| One approximate match; no audit threshold triggered | 167 |
| One approximate match; spatial review suggested | 18 |
| Multiple spawn GUIDs choose the same guide entry | 4 |
| No runtime match observed | 24 |
| Total | 219 |

Six bindings cover #14, #15, #34, #35, #105 and #107. A single approximate match is **not proof** that its number or description belongs to that spawn. The script marks spatial review when the nearest pin is over 15 guide pixels away or its lead over the second pin is less than 2 guide pixels. These are audit triage thresholds only, not game-format evidence or runtime confidence rules.

Duplicate assignments in the original scan (resolved by the follow-up below):

| Duplicated guide | Nearby alternative needing review | Issue |
| --- | --- | --- |
| 73 | 72 | Nearly coincident source pins, same prose, source image/title inconsistency |
| 109 | 108 | Two picnic-table spawns at substantially different heights |
| 118 | 116 | Two sandbox spawns choose the same approximate guide pin |
| 218 | 219 | Two Brawny Bin spawns choose the same approximate guide pin |

These were candidates at the time of the scan. The subsequent user review below supplies the identity evidence. No bijection, candidate subtraction, or height guess was applied.

Other entries flagged for spatial review: **4, 5, 17, 42, 48, 51, 53, 59, 93, 94, 96, 112, 138, 149, 151, 174, 184, 213**.

Entries with no runtime match in this audit: **20, 23, 33, 44, 47, 55, 56, 72, 92, 97, 108, 113, 116, 127, 129, 146, 150, 156, 163, 186, 196, 205, 216, 219**. This means no observed spawn chose that entry; it does not establish an absent or unselected game spawn.

## User-reviewed wiki pairs — follow-up 2026-09-12

The user explicitly confirmed that all directions refer to the wiki guide map, and that #73 is left of #72. The existing audit supplies atomic GUID/XYZ observations. The initial interpretation of above/below reversed the Brawny Bin pair; the user subsequently corrected that swap. The table below records the corrected identities, with that direct correction taking precedence over the earlier directional interpretation:

| Guide | Wiki position / identity | Matched spawn GUID |
| --- | --- | --- |
| 73 | Left, neighboring #72 in the Pond Lab | 879CB034-4B8A7958-C35589AA-EA0E8624 |
| 72 | Right, neighboring #73 in the Pond Lab | 0676F394-4C8EE790-33AE8480-31CFC569 |
| 109 | Left, inside the honey pot | A61209D1-40548155-F232DC96-FA1D7BDB |
| 108 | Right, picnic-table supporting beam | D4138527-4D57599C-937CBA80-0FA0AA16 |
| 118 | Left, inside the salt mines | A9534D87-46471999-D12955B9-840B10BD |
| 116 | Right, Sandbox Lab Outpost requiring Asst Manager Keycard | E9C67113-4D613D18-F58FB089-66BE5191 |
| 218 | Above, between the game box and cardboard box | 9B5FFE39-496231A0-8AB36BB2-43255BB4 |
| 219 | Below, inside the blue waffle ball by the field station | CD10B7F4-43680B03-C33215A6-F38058D6 |

All eight JSON entries now carry explicit bindings. Their existing descriptions already agree with the user's descriptions, so the prose and source pin coordinates are retained. The original scan statistics above remain a historical snapshot; these corrections address all four reported collisions. Other approximate associations remain unverified. Regression checks exercise each identity separately in Uncollected, Collected, and Unknown states. No save data or parser rules were changed.
## Repeating the audit

Run `pwsh -File scripts/Test-Repo.ps1`, then:

```powershell
pwsh -File scripts/Audit-MolarHints.ps1 -SaveFolder "$env:USERPROFILE/Saved Games/Grounded"
```

The script uses the production decoder, validated selected records, and the production hint lookup against the specified JSON. It writes `artifacts/hint-audit/catalog-audit.csv` (all 219 entries), `spawn-matches.csv` (all observed spawn identities and coordinates), and `summary.json`. It never edits the source catalog or saves and does not perform source-web verification automatically. Run outside an outer sandbox if nested AppContainer creation fails; the decoder retains its own isolation.

Regression checks reproduce the original laser/numbering failures and verify their corrections, enforce number uniqueness on load, and confirm that edited file descriptions flow through the actual matching API.
