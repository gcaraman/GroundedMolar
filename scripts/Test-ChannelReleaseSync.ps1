param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '1.0.1'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$storeManifestPath = Join-Path $repositoryRoot "artifacts\store\$Version\store-package-manifest.json"
$releaseManifestPath = Join-Path $repositoryRoot "artifacts\release\$Version\release-manifest.json"

if (-not (Test-Path -LiteralPath $storeManifestPath -PathType Leaf)) {
    throw "Store manifest is missing for version $Version."
}
if (-not (Test-Path -LiteralPath $releaseManifestPath -PathType Leaf)) {
    throw "Nexus/direct release manifest is missing for version $Version."
}

$store = Get-Content -LiteralPath $storeManifestPath -Raw | ConvertFrom-Json
$release = Get-Content -LiteralPath $releaseManifestPath -Raw | ConvertFrom-Json

foreach ($property in @('product', 'version', 'architecture', 'oozSha256')) {
    if ($store.$property -ne $release.$property) {
        throw "Channel mismatch for ${property}: Store '$($store.$property)', Nexus/direct '$($release.$property)'."
    }
}
if ($store.version -ne $Version) { throw "Store manifest version '$($store.version)' did not match $Version." }
if (-not $store.storeSubmissionReady -or $store.directInstallReady) {
    throw 'Store package readiness flags are unsafe.'
}

$nexusArchivePath = Join-Path (Split-Path -Parent $releaseManifestPath) $release.nexusArchive.file
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($nexusArchivePath)
try {
    if ($archive.Entries | Where-Object { $_.FullName -match '\.msix(bundle|upload)?$' }) {
        throw 'The Nexus archive must not contain a Microsoft Store MSIX package.'
    }
}
finally { $archive.Dispose() }

Write-Host "Verified Store and Nexus/direct release synchronization for MolarMap $Version; no MSIX is present in the Nexus archive."
