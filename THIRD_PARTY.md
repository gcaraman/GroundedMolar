# Third-party components

## ooz v7.0

MolarMap invokes `ooz.exe` as a separate process to decode untrusted local Oodle/Kraken payloads inside a no-capability Windows AppContainer with a private decode directory and Job Object resource limits. The reviewed upstream artifact in the repository is pinned to SHA-256 `271D3FD02E582175FF033D0A23DCA3785B6888FA21B8CD06741BA8C19B71DF41`.

Upstream project: <https://github.com/powzix/ooz>

MolarMap redistributes the Windows binary from upstream release v7.0 (commit `0503806`). The upstream source headers license ooz under GNU GPL version 3 or later. The release archive therefore includes the GPL text and this notice; corresponding source is available from the tagged upstream source at <https://github.com/powzix/ooz/tree/v7.0>. MolarMap invokes it as a separate program and does not incorporate its source.

The upstream release does not provide an independently signed checksum or Authenticode signature for this binary. The repository pin therefore records the exact reviewed artifact but is not a claim of reproducible source-to-binary provenance. For trusted public releases, the MolarMap distributor applies an Authenticode signature to a staged copy. That signature authenticates the distributor and exact release bytes; it is not a claim that the distributor authored ooz. The signed helper's changed SHA-256 is compiled into the same app build and recorded in its release manifest.

The decoder warns that it is not fuzz-safe. MolarMap therefore validates strict input/output quotas, snapshots and re-hashes the executable under a launch lock, grants the helper no network capability or access outside one private directory, and kills it on process, memory, CPU, disk, or time-limit violations. Decoder failure is `Unsupported`, never partial analysis.

## Grounded visual material

The map and Milk Molar marker in the app are Grounded-derived fan-project material. Grounded and related marks and assets belong to their respective owners. They are included only for this free, unofficial Grounded companion utility and are not licensed for reuse outside that context.

## Grounded Wiki NG+ Milk Molar guide text

The optional popup hints in `src/GroundedMolar.App/Data/milk-molar-guide.json` are adapted from the 219-marker community interactive map at <https://grounded.fandom.com/wiki/Map:Ng_plus_molar>, accessed 24 August 2026. The source page credits its contributors through the Grounded Wiki revision history and publishes community content under Creative Commons Attribution-ShareAlike. MolarMap bundles only marker numbers, categories, guide coordinates, and short location descriptions; it does not bundle the guide's screenshots. Exact repeated descriptions are retained when the source assigns the same prose to distinct marker coordinates. The hints are labeled as approximate community guide text and remain separate from authoritative save-derived spawn identity and collection state.

## MolarMap marketplace artwork

The marketplace-only `MolarMap-brand-v4.png` artwork was generated with OpenAI image generation under project direction on 26 August 2026, then deterministically resized into the Store logo. It depicts three consistent molar points of interest on an abstract, cropped backyard-map fragment and intentionally contains no full game map, navigation pin, route, compass, GPS imagery, or requested Grounded, Obsidian, Xbox, or Microsoft logo. The installed app, window, and MSI use the original single-molar `MolarMap.ico` with no map background. This disclosure is included so uploaders can apply any generative-AI tag required by a distribution platform.
