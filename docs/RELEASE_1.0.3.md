# MolarMap 1.0.3

Portable release for Windows 10/11 x64. Extract the entire ZIP into a writable folder and run `MolarMap.exe`. No installer or separate .NET installation is required. This release is unsigned, matching the previous portable distribution.

- Updated the bundled Windows Desktop Runtime to 10.0.13 to address the runtime security advisories documented in `docs/DECISIONS.md`.
- Retained the validated save analysis, map projection, community guide hints, and error logging from 1.0.2.

Saves remain read-only. Unrecognized save structures fail closed. Error logs are stored in `logs` beside the application, with `%LOCALAPPDATA%\MolarMap\logs` as a fallback; preferences remain in `%LOCALAPPDATA%\MolarMap\settings.json`.

For an upgrade, extract into a new folder and launch the new executable. Previously distributed self-contained packages require this replacement to receive the bundled runtime update. Remove the old application folder after closing the old version. To uninstall, delete the extracted folder; optionally remove the local preferences and fallback logs.

The same `MolarMap-1.0.3.zip` is used for GitHub and manual Nexus Mods distribution. Verify it against `SHA256SUMS.txt`.
