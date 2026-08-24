# Nexus Mods release preparation

Checked against the Nexus Mods File Submission Guidelines and Terms of Service on 2026-08-23. Re-check them immediately before every public upload because policies can change.

## Prepared listing

- **Game:** Grounded
- **Name:** GroundedMolar — Offline NG+ Milk Molar Map
- **Category:** Utilities
- **Version:** 1.0.0
- **Adult content:** No
- **Requirements:** Grounded for Windows and a local New Game+ `World.csav`; Windows 10/11 x64
- **Installation:** Manual download. Extract the entire ZIP to a writable folder and run `GroundedMolar.exe`. Do not run from inside the ZIP. Vortex is not required because this is a standalone utility and does not install into the game.
- **Uninstallation:** Close the app and delete its extracted folder. Optional settings are stored at `%LOCALAPPDATA%\GroundedMolar\settings.json`.

### Short description

Offline Windows map viewer that reads authoritative Grounded save records and shows uncollected randomized NG+ Milk Molars without modifying the game or save.

### About this mod

GroundedMolar reads a user-selected `World.csav`, reconstructs the save's selected NG+ Milk Molar spawn GUIDs, resolves their persistent collection state, and places validated uncollected markers on a map. It fails closed when a save format is unknown instead of guessing.

The app is completely offline. It has no telemetry, analytics, account requirement, update checker, downloader, uploader, advertising, or network functionality. It reads saves but never writes to or modifies them. It does not inject into Grounded, alter game files, or affect multiplayer.

Extract the full archive, run `GroundedMolar.exe`, and choose either a `World.csav` or a saves folder. Folder monitoring is optional. Back up important saves as a general precaution.

### Known limitations

- Supports only the fixture-validated Grounded World profile described in the project documentation.
- Only selected randomized NG+ Milk Molars are in scope.
- Unknown or changed record signatures display as unsupported and produce no markers.
- Automatic Steam/Xbox save discovery is not yet implemented; select a save or folder manually.
- The map projection is based on the accepted calibration documented by the project and remains subject to the validation gate in `docs/ROADMAP.md`.
- Unsigned executables can trigger Windows SmartScreen reputation warnings. Verify the ZIP hash shown with the release notes and the Nexus virus-scan result.

### Credits

- `ooz` v7.0 by powzix, GNU GPL v3 or later: <https://github.com/powzix/ooz/tree/v7.0>
- Grounded by Obsidian Entertainment / Xbox Game Studios.

### Unofficial-project notice

GroundedMolar isn't endorsed by Obsidian Entertainment, Inc. and doesn't reflect the views or opinions of Obsidian Entertainment, Inc. or anyone officially involved with making Grounded. Grounded and Obsidian Entertainment and all related logos are trademarks or registered trademarks of Obsidian Entertainment, Inc.

## Permissions choices

Use the Nexus permission controls consistently with `LICENSE.txt` and `THIRD_PARTY.md`:

- Do not claim ownership of Grounded-derived visual material or `ooz`.
- Permit redistribution and modification of original GroundedMolar source under GPL-3.0-or-later.
- Require credits for GroundedMolar and all listed third parties.
- State that Grounded-derived material may only be reused where the applicable rights holder permits it.
- Opt out of Donation Points, paid downloads, sponsorship, and direct donations unless written permission is obtained from the relevant Grounded rights holder.

## Mandatory pre-upload checks

1. Confirm `dotnet --info` selects SDK 10.0.303 or a later patched SDK and run `dotnet build` plus `dotnet run --project tests/GroundedMolar.Tests`.
2. Run `powershell -ExecutionPolicy Bypass -File scripts/Publish-Nexus.ps1 -Version 1.0.0`.
   The script deliberately publishes an inspectable, self-contained directory and refuses to create a ZIP unless the actual `PresentationCore.dll`, `PresentationFramework.dll`, and `WindowsBase.dll` product versions are 10.0.11 or newer.
3. Test the extracted ZIP on a clean Windows 10/11 x64 machine with no .NET SDK/runtime installed.
4. Test one supported save and one deliberately unsupported input; confirm the latter renders no markers.
5. Confirm the app creates only `%LOCALAPPDATA%\GroundedMolar\settings.json` and temporary decoder files that are removed after use.
6. Scan the exact ZIP locally, upload it to Nexus, and wait for the Nexus virus scan before making the page public. Explain any false positive; never advise users to disable security software.
7. Publish at least one functioning file with the page—never a placeholder.
8. Include the SHA-256 printed by the packaging script in the file description/changelog.
9. Use only screenshots taken from this app or Grounded that you are entitled to publish; do not use Obsidian or Xbox logos as branding.
10. Re-check the current Nexus File Submission Guidelines, Nexus Terms, Obsidian IP Usage Guidelines, and any Grounded/Xbox terms applicable to the uploader.
11. Confirm the scheduled `Runtime security advisory check` is passing. Before upload, also run `scripts/Test-RuntimeAdvisories.ps1`; update both the runtime pin and the minimum publish gate when Microsoft ships a newer required security floor.
12. Confirm `GroundedMolar.Preview.exe`/`.dll` are absent; the packaging script enforces this because Preview is developer-only and does not share the end-user screenshot validation boundary.
13. Code-sign the app/helper/archive when a project signing identity is available. Until then, state explicitly that the binaries are unsigned, preserve the pinned helper hash/provenance disclosure, and complete SmartScreen plus exact-ZIP malware scanning rather than implying authenticity.

## Compliance assessment

The prepared binary is a functional, offline, non-destructive Windows utility with disclosed requirements and caveats. No registration or user-data submission is required. The project includes third-party credits, a GPL notice, an unofficial-project disclaimer, integrity checking for its decoder, and reproducible self-contained packaging. Publication is still a human act: the uploader must own or have permission for every uploaded component and screenshot, accept Nexus's upload licence, complete malware scanning, choose the permissions above, and validate the ZIP on a clean machine.
