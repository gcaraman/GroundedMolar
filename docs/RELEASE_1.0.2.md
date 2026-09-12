# MolarMap 1.0.2

Portable release for Windows 10/11 x64. Extract the entire ZIP into a writable folder and run `MolarMap.exe`. No installer or separate .NET installation is required. This release is unsigned, matching the 1.0.1 portable distribution.

- Corrected map projection using the exported game bounds and an independently checked oak-lab screenshot.
- Corrected community guide numbers and hints, including spawn-specific bindings for reviewed locations. Approximate community hints do not determine marker positions or collection state.
- Fixed supported saves with empty selected molar groups and recognized underwater molar records.
- Added rotating local error logs to help diagnose save loading and refresh failures.

Saves remain read-only. Unrecognized save structures fail closed. Error logs are stored in `logs` beside the application, with `%LOCALAPPDATA%\MolarMap\logs` as a fallback; preferences remain in `%LOCALAPPDATA%\MolarMap\settings.json`.

For an upgrade, extract into a new folder and launch the new executable. Remove the old application folder after closing the old version. To uninstall, delete the extracted folder; optionally remove the local preferences and fallback logs.

The same `MolarMap-1.0.2.zip` is used for GitHub and manual Nexus Mods distribution. Verify it against `SHA256SUMS.txt`.
