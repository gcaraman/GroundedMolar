param(
    [Parameter(Mandatory = $true)][string]$MsixPath,
    [Parameter(Mandatory = $true)][string]$MakeAppxPath,
    [Parameter(Mandatory = $true)][string]$ExpectedIdentityName,
    [Parameter(Mandatory = $true)][string]$ExpectedPublisher,
    [Parameter(Mandatory = $true)][string]$ExpectedPublisherDisplayName,
    [Parameter(Mandatory = $true)][Version]$ExpectedVersion
)

$ErrorActionPreference = 'Stop'
$resolvedMsix = (Resolve-Path -LiteralPath $MsixPath -ErrorAction Stop).Path
$resolvedMakeAppx = (Resolve-Path -LiteralPath $MakeAppxPath -ErrorAction Stop).Path
$extractRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("MolarMap-msix-test-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $extractRoot | Out-Null
try {
    & $resolvedMakeAppx unpack /p $resolvedMsix /d $extractRoot /o /l
    if ($LASTEXITCODE -ne 0) { throw "MakeAppx failed to unpack the Store package with exit code $LASTEXITCODE." }

    $manifestPath = Join-Path $extractRoot 'AppxManifest.xml'
    [xml]$manifest = Get-Content -LiteralPath $manifestPath -Raw
    $namespaceManager = [System.Xml.XmlNamespaceManager]::new($manifest.NameTable)
    $namespaceManager.AddNamespace('m', 'http://schemas.microsoft.com/appx/manifest/foundation/windows10')
    $namespaceManager.AddNamespace('uap', 'http://schemas.microsoft.com/appx/manifest/uap/windows10')
    $namespaceManager.AddNamespace('rescap', 'http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities')

    $identity = $manifest.SelectSingleNode('/m:Package/m:Identity', $namespaceManager)
    if (-not $identity) { throw 'The Store package has no Identity element.' }
    if ($identity.Name -ne $ExpectedIdentityName) { throw "MSIX identity name '$($identity.Name)' did not match '$ExpectedIdentityName'." }
    if ($identity.Publisher -ne $ExpectedPublisher) { throw "MSIX publisher '$($identity.Publisher)' did not match '$ExpectedPublisher'." }
    if ([Version]$identity.Version -ne $ExpectedVersion) { throw "MSIX version '$($identity.Version)' did not match '$ExpectedVersion'." }
    if ($identity.ProcessorArchitecture -ne 'x64') { throw "MSIX architecture '$($identity.ProcessorArchitecture)' was not x64." }

    $publisherDisplayName = $manifest.SelectSingleNode('/m:Package/m:Properties/m:PublisherDisplayName', $namespaceManager)
    if (-not $publisherDisplayName -or $publisherDisplayName.InnerText -ne $ExpectedPublisherDisplayName) {
        throw 'MSIX PublisherDisplayName did not match the Partner Center value.'
    }
    if (-not $manifest.SelectSingleNode('/m:Package/m:Capabilities/rescap:Capability[@Name="runFullTrust"]', $namespaceManager)) {
        throw 'The packaged WPF application does not declare runFullTrust.'
    }

    foreach ($relativePath in @(
        'MolarMap.exe', 'MolarMap.dll', 'GroundedMolar.Core.dll', 'ooz.exe',
        'PackageAssets\StoreLogo.png', 'PackageAssets\Square44x44Logo.png',
        'PackageAssets\Square150x150Logo.png')) {
        if (-not (Test-Path -LiteralPath (Join-Path $extractRoot $relativePath) -PathType Leaf)) {
            throw "Store package is incomplete: $relativePath is missing."
        }
    }

    $executable = Join-Path $extractRoot 'MolarMap.exe'
    $actualVersion = [Version](([System.Diagnostics.FileVersionInfo]::GetVersionInfo($executable).FileVersion -split '[+-]', 2)[0])
    if ($actualVersion -ne $ExpectedVersion) { throw "Packaged executable version $actualVersion did not match $ExpectedVersion." }
    Write-Host "Verified Store MSIX identity, publisher, x64 architecture, full-trust declaration, assets, and version $actualVersion."
}
finally {
    if (Test-Path -LiteralPath $extractRoot) { Remove-Item -LiteralPath $extractRoot -Recurse -Force }
}
