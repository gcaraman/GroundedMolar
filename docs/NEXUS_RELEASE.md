# Nexus Mods release preparation

Checked against the Nexus Mods File Submission Guidelines and Terms of Service on 2026-08-26. Re-check them immediately before every public upload because policies can change.

## Prepared listing

- **Game:** Grounded
- **Name:** MolarMap — Offline NG+ Milk Molar Map
- **Category:** Utilities
- **Version:** 1.0.1
- **Adult content:** No
- **Generative AI tag:** Yes — the marketplace preview artwork was generated with OpenAI image generation; the installed app retains its original single-molar icon, and application behavior, save analysis, map reconstruction, and community hint data are not generated content.
- **Requirements:** Grounded for Windows and a local New Game+ `World.csav`; Windows 10/11 x64
- **Installation:** Manual download. Extract the ZIP and run the included signed `MolarMap-1.0.1-win-x64.msi`. Vortex is not required because this is a standalone utility and does not install into the game.
- **Uninstallation:** Use Windows Settings → Apps → Installed apps → MolarMap → Uninstall. Optional user settings remain at `%LOCALAPPDATA%\MolarMap\settings.json` and may be deleted manually.

### Short description

Offline Windows map viewer that reads authoritative Grounded save records and shows uncollected randomized NG+ Milk Molars without modifying the game or save.

### About this mod

MolarMap reads a user-selected `World.csav`, reconstructs the save's selected NG+ Milk Molar spawn GUIDs, resolves their persistent collection state, and places validated uncollected markers on a map. It fails closed when a save format is unknown instead of guessing.

The app is completely offline. It has no telemetry, analytics, account requirement, update checker, downloader, uploader, advertising, or network functionality. It reads saves but never writes to or modifies them. It does not inject into Grounded, alter game files, or affect multiplayer.

Extract the archive, run the signed MSI, open MolarMap from the Start menu, and choose either a `World.csav` or a saves folder. Folder monitoring is optional. Back up important saves as a general precaution.

### Known limitations

- Supports only the fixture-validated Grounded World profile described in the project documentation.
- Only selected randomized NG+ Milk Molars are in scope.
- Unknown or changed record signatures display as unsupported and produce no markers.
- Automatic Steam/Xbox save discovery is not yet implemented; select a save or folder manually.
- The map projection is based on the accepted calibration documented by the project and remains subject to the validation gate in `docs/ROADMAP.md`.
- The MSI and executable payload are Authenticode-signed and timestamped with the publisher identity shown by Windows. A new publisher may still receive temporary SmartScreen reputation prompts. Verify the MSI/ZIP hash shown with the release notes and the Nexus virus-scan result.

### Credits

- `ooz` v7.0 by powzix, GNU GPL v3 or later: <https://github.com/powzix/ooz/tree/v7.0>
- Grounded by Obsidian Entertainment / Xbox Game Studios.

### Unofficial-project notice

MolarMap isn't endorsed by Obsidian Entertainment, Inc. and doesn't reflect the views or opinions of Obsidian Entertainment, Inc. or anyone officially involved with making Grounded. Grounded and Obsidian Entertainment and all related logos are trademarks or registered trademarks of Obsidian Entertainment, Inc.

## Permissions choices

Use the Nexus permission controls consistently with `LICENSE.txt` and `THIRD_PARTY.md`:

- Do not claim ownership of Grounded-derived visual material or `ooz`.
- Permit redistribution and modification of original MolarMap source under GPL-3.0-or-later.
- Require credits for MolarMap and all listed third parties.
- State that Grounded-derived material may only be reused where the applicable rights holder permits it.
- Opt out of Donation Points, paid downloads, sponsorship, and direct donations unless written permission is obtained from the relevant Grounded rights holder.

## Mandatory pre-upload checks

1. Confirm `dotnet --info` selects SDK 10.0.303 or a later patched SDK and run `dotnet build` plus `dotnet run --project tests/GroundedMolar.Tests`.
2. Run the signed production command in `docs/RELEASE_1.0.1.md`. Never upload an artifact containing `UNSIGNED-DO-NOT-PUBLISH` or a manifest with `productionReady: false`.
   The script publishes an inspectable self-contained payload, signs the exact helper before compiling its hash pin, builds the common MSI, and refuses production completion unless every PE and the MSI has a valid trusted signature. It also requires the actual `PresentationCore.dll`, `PresentationFramework.dll`, and `WindowsBase.dll` product versions to be 10.0.11 or newer.
3. Test the exact MSI from the extracted ZIP on a clean Windows 10/11 x64 machine with no .NET SDK/runtime installed. Confirm silent install and uninstall as well as interactive launch.
4. Test one supported save and one deliberately unsupported input; confirm the latter renders no markers.
5. Confirm the app creates only `%LOCALAPPDATA%\MolarMap\settings.json` and temporary decoder files that are removed after use. Existing `%LOCALAPPDATA%\GroundedMolar\settings.json` preferences may be read once for migration.
6. Scan the exact ZIP locally, upload it to Nexus, and wait for the Nexus virus scan before making the page public. Explain any false positive; never advise users to disable security software.
7. Publish at least one functioning file with the page—never a placeholder.
8. Include the SHA-256 printed by the packaging script in the file description/changelog.
9. Use only screenshots taken from this app or Grounded that you are entitled to publish; do not use Obsidian or Xbox logos as branding.
10. Re-check the current Nexus File Submission Guidelines, Nexus Terms, Obsidian IP Usage Guidelines, and any Grounded/Xbox terms applicable to the uploader.
11. Confirm the scheduled `Runtime security advisory check` is passing. Before upload, also run `scripts/Test-RuntimeAdvisories.ps1`; update both the runtime pin and the minimum publish gate when Microsoft ships a newer required security floor.
12. Verify the downloaded MSI's publisher, RFC 3161 timestamp, SHA-256, helper pin, and all embedded PE signatures. The ZIP itself is hash-published rather than Authenticode-signed; the signed MSI inside is the installation trust boundary.
13. Confirm the Nexus ZIP and Store MSIX were built from the same reviewed source revision and carry the same product version, runtime floor, functionality, and user-visible release notes. Never upload the unsigned pre-certification Store MSIX to Nexus.

## Compliance assessment

The prepared installer is a functional, offline, non-destructive Windows utility with disclosed requirements and caveats. No registration or user-data submission is required. The project includes third-party credits, a GPL notice, an unofficial-project disclaimer, integrity checking for its decoder, a signing-enforced dual-channel pipeline, and reproducible self-contained packaging. Publication is still a human act: the uploader must own or have permission for every uploaded component and screenshot, accept Nexus's upload licence, complete malware scanning, choose the permissions above, and validate the MSI/ZIP on a clean machine.
