param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '1.0.1',
    [string]$PackageIdentityName = 'Gweko.MolarMap',
    [string]$PackagePublisher = 'CN=7F7844FD-9C33-43BF-91A4-548741E63E59',
    [string]$PublisherDisplayName = 'Gweko',
    [string]$PackageFamilyName = 'Gweko.MolarMap_7tz02chjy4n5g',
    [string]$PackageSid = 'S-1-15-2-4197226052-1525588469-4159657442-1346264295-3646779232-572381210-2898514796',
    [string]$StoreId = '9N8MX9MHS3XD'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'The Store pipeline requires PowerShell 7 or newer.' }
$repositoryRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'Release-Signing.ps1')

$storeArtifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts\store'))
$releaseRoot = [System.IO.Path]::GetFullPath((Join-Path $storeArtifactsRoot $Version))
$comparison = [System.StringComparison]::OrdinalIgnoreCase
if (-not ($releaseRoot + [System.IO.Path]::DirectorySeparatorChar).StartsWith($storeArtifactsRoot + [System.IO.Path]::DirectorySeparatorChar, $comparison)) {
    throw 'The computed Store release directory escaped artifacts\store.'
}
$stagingRoot = Join-Path $releaseRoot 'staging'
$packageRoot = Join-Path $stagingRoot 'package'
$dotnetArtifactsRoot = Join-Path $stagingRoot 'dotnet'
$msixPath = Join-Path $releaseRoot "MolarMap-$Version-x64-store.msix"
$manifestOutputPath = Join-Path $releaseRoot 'store-package-manifest.json'
$checksumsPath = Join-Path $releaseRoot 'SHA256SUMS.txt'

if (Test-Path -LiteralPath $releaseRoot) { Remove-Item -LiteralPath $releaseRoot -Recurse -Force }
New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null

$privateDotnetCliHome = Join-Path $repositoryRoot '.dotnet-cli'
$privateNugetPackages = Join-Path $repositoryRoot '.nuget\packages'
$privateAppData = Join-Path $repositoryRoot '.appdata'
New-Item -ItemType Directory -Path $privateDotnetCliHome, $privateNugetPackages, $privateAppData -Force | Out-Null

function Invoke-StoreRegressionTests {
    for ($attempt = 1; $attempt -le 2; $attempt++) {
        & dotnet run --project (Join-Path $repositoryRoot 'tests\GroundedMolar.Tests') --configuration Release `
            --no-build --artifacts-path $dotnetArtifactsRoot
        if ($LASTEXITCODE -eq 0) { return }
        if ($attempt -eq 1) {
            Write-Warning 'Regression run hit a transient AppContainer launch failure; retrying once in a fresh test process.'
            Start-Sleep -Seconds 5
        }
    }
    throw 'Regression tests failed twice.'
}

$makeAppx = Get-ChildItem -LiteralPath (Join-Path $privateNugetPackages 'microsoft.windows.sdk.buildtools\10.0.28000.2526') `
    -Filter makeappx.exe -File -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '[\\/]x64[\\/]makeappx\.exe$' } |
    Sort-Object FullName -Descending |
    Select-Object -First 1
if (-not $makeAppx) {
    $restoreExitCode = Invoke-ReleaseDotNet -RepositoryRoot $repositoryRoot -Arguments @(
        'restore', (Join-Path $repositoryRoot 'build\ReleaseTools.csproj'),
        '--configfile', (Join-Path $repositoryRoot 'build\NuGet.Config'),
        '--locked-mode')
    if ($restoreExitCode -ne 0) { throw 'Failed to restore the pinned Windows SDK packaging tools.' }
    $makeAppx = Get-ChildItem -LiteralPath (Join-Path $privateNugetPackages 'microsoft.windows.sdk.buildtools\10.0.28000.2526') `
        -Filter makeappx.exe -File -Recurse |
        Where-Object { $_.FullName -match '[\\/]x64[\\/]makeappx\.exe$' } |
        Sort-Object FullName -Descending |
        Select-Object -First 1
}
if (-not $makeAppx) { throw 'The pinned Windows SDK package did not contain x64 MakeAppx.' }

Write-Host "Building Microsoft Store MSIX for MolarMap $Version."
$buildExitCode = Invoke-ReleaseDotNet -RepositoryRoot $repositoryRoot -Arguments @(
    'build', (Join-Path $repositoryRoot 'MolarMap.slnx'),
    '--configuration', 'Release',
    '--artifacts-path', $dotnetArtifactsRoot,
    '--source', 'https://api.nuget.org/v3/index.json',
    '--configfile', (Join-Path $repositoryRoot 'NuGet.Config'))
if ($buildExitCode -ne 0) { throw 'Store release build failed.' }

Invoke-StoreRegressionTests

$oozPath = Join-Path $repositoryRoot 'ooz.exe'
$oozHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $oozPath).Hash
$publishExitCode = Invoke-ReleaseDotNet -RepositoryRoot $repositoryRoot -Arguments @(
    'publish', (Join-Path $repositoryRoot 'src\GroundedMolar.App\GroundedMolar.App.csproj'),
    '--configuration', 'Release',
    '--runtime', 'win-x64',
    '--self-contained', 'true',
    "-p:Version=$Version",
    "-p:ApplicationVersion=$Version.0",
    "-p:Company=$PublisherDisplayName",
    "-p:OozSourcePath=$oozPath",
    "-p:OozPinnedSha256=$oozHash",
    '-p:DebugType=None',
    '-p:PublishSingleFile=false',
    '--artifacts-path', $dotnetArtifactsRoot,
    '--source', 'https://api.nuget.org/v3/index.json',
    '--configfile', (Join-Path $repositoryRoot 'NuGet.Config'),
    '--output', $packageRoot)
if ($publishExitCode -ne 0) { throw 'Store app publish failed.' }

Copy-Item -LiteralPath (Join-Path $repositoryRoot 'README.md') -Destination $packageRoot
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'LICENSE.txt') -Destination $packageRoot
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'THIRD_PARTY.md') -Destination $packageRoot
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'PRIVACY.md') -Destination $packageRoot
& (Join-Path $PSScriptRoot 'Prepare-MsixPackageAssets.ps1') -DestinationDirectory (Join-Path $packageRoot 'PackageAssets')

$packageVersion = "$Version.0"
$template = Get-Content -LiteralPath (Join-Path $repositoryRoot 'store-package\AppxManifest.xml.template') -Raw
$manifestText = $template.Replace('{{PACKAGE_IDENTITY_NAME}}', [System.Security.SecurityElement]::Escape($PackageIdentityName))
$manifestText = $manifestText.Replace('{{PACKAGE_PUBLISHER}}', [System.Security.SecurityElement]::Escape($PackagePublisher))
$manifestText = $manifestText.Replace('{{PUBLISHER_DISPLAY_NAME}}', [System.Security.SecurityElement]::Escape($PublisherDisplayName))
$manifestText = $manifestText.Replace('{{PACKAGE_VERSION}}', $packageVersion)
if ($manifestText -match '\{\{[^}]+\}\}') { throw 'The Store manifest still contains an unresolved template token.' }
$manifestText | Set-Content -LiteralPath (Join-Path $packageRoot 'AppxManifest.xml') -Encoding UTF8

& (Join-Path $PSScriptRoot 'Test-PublishedRuntime.ps1') -PublishDirectory $packageRoot
if ((Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $packageRoot 'ooz.exe')).Hash -ne $oozHash) {
    throw 'The Store payload helper does not match the hash compiled into the application.'
}

& $makeAppx.FullName pack /d $packageRoot /p $msixPath /o /l
if ($LASTEXITCODE -ne 0) { throw "MakeAppx failed with exit code $LASTEXITCODE." }
& (Join-Path $PSScriptRoot 'Test-StoreMsixPackage.ps1') -MsixPath $msixPath -MakeAppxPath $makeAppx.FullName `
    -ExpectedIdentityName $PackageIdentityName -ExpectedPublisher $PackagePublisher `
    -ExpectedPublisherDisplayName $PublisherDisplayName -ExpectedVersion ([Version]$packageVersion)

$msixHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $msixPath).Hash
$storeManifest = [ordered]@{
    product = 'MolarMap'
    version = $Version
    architecture = 'x64'
    createdUtc = [DateTime]::UtcNow.ToString('o')
    storeSubmissionReady = $true
    directInstallReady = $false
    signing = 'Microsoft Store signs the MSIX after certification'
    packageIdentityName = $PackageIdentityName
    packagePublisher = $PackagePublisher
    publisherDisplayName = $PublisherDisplayName
    packageFamilyName = $PackageFamilyName
    packageSid = $PackageSid
    storeId = $StoreId
    oozSha256 = $oozHash
    package = [ordered]@{ file = (Split-Path -Leaf $msixPath); sha256 = $msixHash }
}
$storeManifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $manifestOutputPath -Encoding UTF8
"$msixHash  $(Split-Path -Leaf $msixPath)" | Set-Content -LiteralPath $checksumsPath -Encoding ASCII
@(
    'MICROSOFT STORE SUBMISSION PACKAGE',
    'Upload this MSIX to Partner Center. It is intentionally not signed for direct sideloading.',
    'Microsoft applies the trusted Store signature after certification.',
    'Do not distribute this unsigned pre-certification file through Nexus or direct download.'
) | Set-Content -LiteralPath (Join-Path $releaseRoot 'STORE-SUBMISSION-ONLY.txt') -Encoding ASCII

Remove-Item -LiteralPath $stagingRoot -Recurse -Force
if (Test-Path -LiteralPath (Join-Path $repositoryRoot "artifacts\release\$Version\release-manifest.json")) {
    & (Join-Path $PSScriptRoot 'Test-ChannelReleaseSync.ps1') -Version $Version
}
Write-Host "Created Store submission package: $msixPath"
Write-Host "MSIX SHA-256: $msixHash"
Write-Warning 'This pre-certification MSIX is for Partner Center upload only; Microsoft signs it after certification.'
