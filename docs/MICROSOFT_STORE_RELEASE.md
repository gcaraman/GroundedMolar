# Microsoft Store release preparation — MolarMap 1.0.1

Checked against Microsoft's MSIX Store submission documentation on 28 August 2026. Re-check immediately before submission.

## Distribution model

Use the Microsoft Store **MSIX/PWA app** path and upload the submission-only MSIX produced by `scripts/Publish-StoreMsix.ps1`. Microsoft signs the package after certification. Do not Authenticode-sign or distribute this pre-certification MSIX through Nexus or direct download.

- Store ID: `9N8MX9MHS3XD`
- Package type: MSIX
- Architecture: x64
- Package version: 1.0.1.0
- Identity name: `Gweko.MolarMap`
- Publisher: `CN=7F7844FD-9C33-43BF-91A4-548741E63E59`
- Publisher display name: `Gweko`
- Package family name: `Gweko.MolarMap_7tz02chjy4n5g`
- Package SID: `S-1-15-2-4197226052-1525588469-4159657442-1346264295-3646779232-572381210-2898514796`
- Submission package: `artifacts/store/1.0.1/MolarMap-1.0.1-x64-store.msix`

The Nexus release remains version- and content-aligned but uses its separately signed MSI/ZIP. No Nexus upload is part of the Store submission.

## Prepared listing

- Product name: MolarMap (reserve this exact name in Partner Center)
- Category: Utilities & tools
- Pricing: Free
- Markets: Select the markets in which the publisher is legally able to distribute the included third-party and Grounded-derived material.
- Minimum OS: Windows 10 x64
- Recommended OS: Windows 11 x64
- Required hardware: x64 PC
- Additional requirement: Grounded for Windows and access to a local New Game+ `World.csav`
- Contact information: **publisher must supply a monitored public support email or HTTPS support page**
- Privacy policy URL: **publish `PRIVACY.md` at a stable public HTTPS URL and enter that URL**

### Short description

Offline map viewer that reads authoritative Grounded save records and shows uncollected randomized New Game+ Milk Molars.

### Description

MolarMap helps Grounded players locate the randomized Milk Molars selected for their New Game+ world. Choose a local `World.csav` or saves folder and MolarMap reconstructs the selected spawn GUIDs, resolves their persistent collection state, and displays only validated uncollected markers on an offline map.

The app fails closed when it encounters an unknown or changed save structure. It never guesses from landmarks, icon colors, proximity, or candidate-list subtraction. It does not modify saves or game files, inject into Grounded, connect to multiplayer, require an account, or transmit data.

Optional community guide hints add approximate location prose to marker popups. These hints are clearly separated from the authoritative save-derived marker state.

MolarMap is a free, unofficial fan project. It is not endorsed by Obsidian Entertainment or Xbox Game Studios.

### Product features

Enter each line as a separate Partner Center feature without adding bullet characters:

```text
Reads authoritative selected-spawn GUIDs from local Grounded saves
Shows validated uncollected randomized New Game+ Milk Molars
Runs completely offline with no telemetry or account
Never modifies Grounded saves or game files
Fails closed for unknown or changed save formats
Supports direct save selection and recursive folder monitoring
Includes optional, clearly labeled community location hints
```

### Version 1.0.1 release notes

Initial trusted public release of MolarMap. Includes authoritative save-derived NG+ Milk Molar state, offline map display, save/folder selection, folder monitoring, map zoom and panning, adjustable unapproached-marker opacity, optional community hints, and a sandboxed integrity-pinned Kraken decoder.

## Visual assets

- Upload `store-assets/MolarMap-StoreLogo-300x300.png` as the 1:1 Store logo.
- Add at least one real 1366 × 768-or-larger PC screenshot. Four or more are recommended.
- Follow `store-assets/README.md`; never expose private save paths or fixtures.

## Certification notes

MolarMap is a self-contained WPF desktop utility packaged as x64 MSIX. It declares `runFullTrust` because WPF needs packaged-classic desktop execution and because the user explicitly chooses local save files or folders outside the package. It has no network capability, telemetry, account requirement, updater, service, driver, shell extension, or background task. User preferences are stored locally under `%LOCALAPPDATA%\MolarMap`.

The bundled `ooz.exe` helper is third-party GPL-3.0-or-later software. MolarMap pins its exact reviewed SHA-256 and executes it in a no-capability AppContainer with network denial and resource limits. Microsoft signs the complete MSIX after certification; the pre-certification Store payload is not individually Authenticode-signed.

## Human/account gates before submission

1. Confirm the reserved Store product matches Store ID `9N8MX9MHS3XD` and the identity fields above.
2. Run `pwsh -File scripts/Publish-StoreMsix.ps1 -Version 1.0.1` from the reviewed source revision.
3. Run the Windows App Certification Kit against the exact package and scan it with current Microsoft Defender definitions.
4. Supply the support URL/email and hosted privacy-policy URL.
5. Add at least one clean real screenshot; four or more are recommended.
6. Complete pricing/availability, properties, age ratings, Store listing, and required business contact fields.
7. Upload `MolarMap-1.0.1-x64-store.msix`, resolve every validation error, and confirm only desktop/x64 availability.
8. Complete the restricted-capability justification for `runFullTrust` using the certification notes above.
9. Choose the intended publishing hold behavior, then submit for certification.
10. Keep the Nexus release locally synchronized to version 1.0.1 and the same notes, but do not upload it until separately requested and its signed production gates pass.

References: [upload MSIX packages](https://learn.microsoft.com/windows/apps/publish/publish-your-app/msix/upload-app-packages), [MSIX package requirements](https://learn.microsoft.com/windows/apps/publish/publish-your-app/msix/app-package-requirements), [MSIX submission checklist](https://learn.microsoft.com/windows/apps/publish/publish-your-app/msix/create-app-submission), and [MSIX certification process](https://learn.microsoft.com/windows/apps/publish/publish-your-app/msix/app-certification-process).
