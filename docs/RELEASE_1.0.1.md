# Trusted dual-channel release runbook — MolarMap 1.0.1

Build both channels from the same reviewed source revision. Microsoft Store receives the x64 MSIX produced by `scripts/Publish-StoreMsix.ps1`; Nexus/direct download receives the separately signed offline x64 MSI inside the ZIP produced by `scripts/Publish-Release.ps1`. Keep product version, runtime floor, functionality, and release notes aligned. Never distribute the unsigned pre-certification Store MSIX through Nexus.

## Microsoft Store package

```powershell
pwsh -File scripts/Publish-StoreMsix.ps1 -Version 1.0.1
```

This runs the build and regression gates, publishes the self-contained x64 payload, validates the WPF runtime floor, generates package assets, creates the Partner Center manifest identity, packs the MSIX, unpacks it again, and verifies identity, publisher, version, architecture, full-trust declaration, and required files. Output is under `artifacts/store/1.0.1`. Microsoft signs the package after certification; the local MSIX is submission-only.

## Signing options

Use a publicly trusted RSA code-signing identity. Self-signed certificates deliberately fail the production trust gate.

### Certificate exposed through the Windows certificate store

```powershell
pwsh -File scripts/Publish-Release.ps1 `
  -Version 1.0.1 `
  -SigningMode CertificateStore `
  -CertificateThumbprint '<40-hex-thumbprint>'
```

The publisher name is taken from the signing certificate unless `-PublisherName` is supplied. The default store is `Cert:\CurrentUser\My`; add `-CertificateStoreScope LocalMachine` when the issuer exposes the hardware-backed certificate through `Cert:\LocalMachine\My`.

### Microsoft Artifact Signing

```powershell
pwsh -File scripts/Publish-Release.ps1 `
  -Version 1.0.1 `
  -SigningMode ArtifactSigning `
  -PublisherName '<verified public identity>' `
  -ArtifactSigningMetadataPath '<metadata.json>' `
  -ArtifactSigningDlibPath '<Azure.CodeSigning.Dlib.dll>'
```

Keep Azure credentials outside the repository. `metadata.json` identifies the Artifact Signing endpoint/account/profile and must not contain a client secret.

### Local packaging rehearsal

```powershell
pwsh -File scripts/Publish-Release.ps1 -Version 1.0.1
```

This creates artifacts whose names contain `UNSIGNED-DO-NOT-PUBLISH`, sets `productionReady` to false, and writes `DO-NOT-PUBLISH.txt`. They exist only to test the build/MSI pipeline.

## Automated gates

The production pipeline:

1. Builds and runs all regression tests in an isolated output tree.
2. Copies and signs `ooz.exe` before compiling the app.
3. Compiles the exact signed helper SHA-256 into `GroundedMolar.Core.dll` and re-runs tests against it.
4. Publishes the self-contained Windows x64 payload and checks the pinned WPF runtime security floor.
5. Signs every previously unsigned `.exe` and `.dll`; rejects invalid pre-existing signatures.
6. Verifies every PE payload has a valid Authenticode signature.
7. Builds one embedded-cab MSI, signs and timestamps it, and verifies its administrative extraction and version.
8. Produces the Nexus ZIP, release manifest, and SHA-256 list.

Production Nexus/direct artifacts are under `artifacts/release/1.0.1`.

## Manual gates

- Run `scripts/Test-RuntimeAdvisories.ps1` immediately before release.
- Scan the exact MSI and Nexus ZIP with current Microsoft Defender definitions.
- Install silently on a standard clean Windows 10/11 x64 VM with no .NET runtime; launch, test supported/unsupported inputs, then uninstall silently and inspect the result.
- Test Windows 11 SmartScreen and Smart App Control with the downloaded artifacts.
- Confirm the Nexus MSI signature publisher, SHA-256 values, version 1.0.1, and timestamp after downloading it from Nexus/direct hosting.
- Confirm the Store MSIX and Nexus ZIP were built from the same reviewed source revision and share version 1.0.1 plus the same release notes.
- Complete the channel-specific checklists in `docs/NEXUS_RELEASE.md` and `docs/MICROSOFT_STORE_RELEASE.md`.

Do not publish if `release-manifest.json` says `productionReady: false`, any signature is not valid, the helper hash differs, a platform malware scan is unresolved, or the clean-machine checks have not passed.
