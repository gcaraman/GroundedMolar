param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '1.0.1',
    [ValidateSet('None', 'CertificateStore', 'ArtifactSigning')]
    [string]$SigningMode = 'None',
    [string]$PublisherName,
    [string]$CertificateThumbprint,
    [ValidateSet('CurrentUser', 'LocalMachine')]
    [string]$CertificateStoreScope = 'CurrentUser',
    [string]$ArtifactSigningMetadataPath,
    [string]$ArtifactSigningDlibPath,
    [string]$SignToolPath,
    [string]$TimestampUrl = 'http://timestamp.acs.microsoft.com',
    [switch]$SkipRegressionTestsForPackagingRehearsal
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($PSVersionTable.PSVersion.Major -lt 7) {
    throw 'The trusted release pipeline requires PowerShell 7 or newer. Run: pwsh -File scripts/Publish-Release.ps1 ...'
}
$repositoryRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'Release-Signing.ps1')

$artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts\release'))
$releaseRoot = [System.IO.Path]::GetFullPath((Join-Path $artifactsRoot $Version))
$comparison = [System.StringComparison]::OrdinalIgnoreCase
if (-not ($releaseRoot + [System.IO.Path]::DirectorySeparatorChar).StartsWith($artifactsRoot + [System.IO.Path]::DirectorySeparatorChar, $comparison)) {
    throw 'The computed release directory escaped artifacts\release.'
}
$stagingRoot = Join-Path $releaseRoot 'staging'
$payloadRoot = Join-Path $stagingRoot 'payload'
$installerBuildRoot = Join-Path $stagingRoot 'installer'
$nexusRoot = Join-Path $stagingRoot 'nexus'
$dotnetArtifactsRoot = Join-Path $stagingRoot 'dotnet'
$signedRelease = $SigningMode -ne 'None'
if ($signedRelease -and $SkipRegressionTestsForPackagingRehearsal) {
    throw '-SkipRegressionTestsForPackagingRehearsal is forbidden for signed releases.'
}
$artifactQualifier = if ($signedRelease) { '' } else { '-UNSIGNED-DO-NOT-PUBLISH' }
$artifactBaseName = "MolarMap-$Version-win-x64$artifactQualifier"
$msiPath = Join-Path $releaseRoot "$artifactBaseName.msi"
$nexusArchivePath = Join-Path $releaseRoot "$artifactBaseName-nexus.zip"
$manifestPath = Join-Path $releaseRoot 'release-manifest.json'
$checksumsPath = Join-Path $releaseRoot 'SHA256SUMS.txt'

$privateDotnetCliHome = Join-Path $repositoryRoot '.dotnet-cli'
$privateNugetPackages = Join-Path $repositoryRoot '.nuget\packages'
New-Item -ItemType Directory -Path $privateDotnetCliHome, $privateNugetPackages -Force | Out-Null
$nugetConfigPath = Join-Path $repositoryRoot 'NuGet.Config'
$wixNugetConfigPath = Join-Path $repositoryRoot 'installer\NuGet.Config'
$privateAppData = Join-Path $repositoryRoot '.appdata'
New-Item -ItemType Directory -Path $privateAppData -Force | Out-Null

function Invoke-RegressionTests {
    param([Parameter(Mandatory = $true)][string]$ArtifactsPath)

    for ($attempt = 1; $attempt -le 2; $attempt++) {
        & dotnet run --project (Join-Path $repositoryRoot 'tests\GroundedMolar.Tests') --configuration Release `
            --no-build --artifacts-path $ArtifactsPath
        if ($LASTEXITCODE -eq 0) { return }
        if ($attempt -eq 1) {
            Write-Warning 'Regression run failed; retrying once after the documented transient AppContainer profile delay.'
            Start-Sleep -Seconds 5
        }
    }
    throw 'Regression tests failed twice.'
}

if ($signedRelease) {
    & (Join-Path $PSScriptRoot 'Test-RuntimeAdvisories.ps1')
    $SignToolPath = Find-ReleaseSignTool -ExplicitPath $SignToolPath -RepositoryRoot $repositoryRoot
    if ($SigningMode -eq 'CertificateStore') {
        if (-not $CertificateThumbprint) { throw 'CertificateStore signing requires -CertificateThumbprint.' }
        $normalizedThumbprint = $CertificateThumbprint -replace '[^0-9A-Fa-f]', ''
        $certificateStorePath = "Cert:\$CertificateStoreScope\My"
        $certificate = Get-ChildItem -Path $certificateStorePath -ErrorAction SilentlyContinue |
            Where-Object { $_.Thumbprint -eq $normalizedThumbprint } |
            Select-Object -First 1
        if (-not $certificate) { throw "The signing certificate $normalizedThumbprint was not found in $certificateStorePath." }
        if (-not $certificate.HasPrivateKey) { throw 'The selected signing certificate has no accessible private key.' }
        if (-not $PublisherName) { $PublisherName = $certificate.GetNameInfo([System.Security.Cryptography.X509Certificates.X509NameType]::SimpleName, $false) }
    }
    elseif (-not $PublisherName) {
        throw 'ArtifactSigning releases require -PublisherName matching the verified public identity.'
    }
}
elseif (-not $PublisherName) {
    $PublisherName = 'MolarMap contributors'
}

if (Test-Path -LiteralPath $releaseRoot) { Remove-Item -LiteralPath $releaseRoot -Recurse -Force }
New-Item -ItemType Directory -Path $payloadRoot, $installerBuildRoot, $nexusRoot -Force | Out-Null
& (Join-Path $PSScriptRoot 'Prepare-StoreAssets.ps1')

Write-Host "Building MolarMap $Version ($SigningMode)."
$dotnetExitCode = Invoke-ReleaseDotNet -RepositoryRoot $repositoryRoot -Arguments @(
    'build', (Join-Path $repositoryRoot 'MolarMap.slnx'),
    '--configuration', 'Release',
    '--artifacts-path', $dotnetArtifactsRoot,
    '--source', 'https://api.nuget.org/v3/index.json',
    '--configfile', $nugetConfigPath)
if ($dotnetExitCode -ne 0) { throw 'dotnet build failed.' }
if (-not $SkipRegressionTestsForPackagingRehearsal) { Invoke-RegressionTests -ArtifactsPath $dotnetArtifactsRoot }

$releaseOozPath = Join-Path $stagingRoot 'ooz.exe'
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'ooz.exe') -Destination $releaseOozPath
if ($signedRelease) {
    Invoke-ReleaseSignature -SignToolPath $SignToolPath -SigningMode $SigningMode -Path @($releaseOozPath) `
        -CertificateThumbprint $CertificateThumbprint -CertificateStoreScope $CertificateStoreScope `
        -ArtifactSigningMetadataPath $ArtifactSigningMetadataPath `
        -ArtifactSigningDlibPath $ArtifactSigningDlibPath -TimestampUrl $TimestampUrl
    Assert-ValidAuthenticodeSignature -Path @($releaseOozPath)
}
$oozHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $releaseOozPath).Hash

$previousOozPath = $env:GROUNDED_OOZ_PATH
$previousOozPin = $env:GROUNDED_EXPECTED_OOZ_PIN
try {
    $env:GROUNDED_OOZ_PATH = $releaseOozPath
    $env:GROUNDED_EXPECTED_OOZ_PIN = $oozHash
    if (-not $SkipRegressionTestsForPackagingRehearsal) {
        $releasePinArtifactsRoot = Join-Path $dotnetArtifactsRoot 'release-pin'
        $dotnetExitCode = Invoke-ReleaseDotNet -RepositoryRoot $repositoryRoot -Arguments @(
            'build', (Join-Path $repositoryRoot 'tests\GroundedMolar.Tests\GroundedMolar.Tests.csproj'),
            '--configuration', 'Release',
            '--artifacts-path', $releasePinArtifactsRoot,
            "-p:OozPinnedSha256=$oozHash",
            '--source', 'https://api.nuget.org/v3/index.json',
            '--configfile', $nugetConfigPath)
        if ($dotnetExitCode -ne 0) { throw 'Release-helper pin test build failed.' }
        Invoke-RegressionTests -ArtifactsPath $releasePinArtifactsRoot
    }
}
finally {
    $env:GROUNDED_OOZ_PATH = $previousOozPath
    $env:GROUNDED_EXPECTED_OOZ_PIN = $previousOozPin
}

$dotnetExitCode = Invoke-ReleaseDotNet -RepositoryRoot $repositoryRoot -Arguments @(
    'publish', (Join-Path $repositoryRoot 'src\GroundedMolar.App\GroundedMolar.App.csproj'),
    '--configuration', 'Release',
    '--runtime', 'win-x64',
    '--self-contained', 'true',
    "-p:Version=$Version",
    "-p:ApplicationVersion=$Version.0",
    "-p:Company=$PublisherName",
    "-p:OozSourcePath=$releaseOozPath",
    "-p:OozPinnedSha256=$oozHash",
    '-p:DebugType=None',
    '-p:PublishSingleFile=false',
    '--artifacts-path', $dotnetArtifactsRoot,
    '--source', 'https://api.nuget.org/v3/index.json',
    '--configfile', $nugetConfigPath,
    '--output', $payloadRoot)
if ($dotnetExitCode -ne 0) { throw 'dotnet publish failed.' }

Copy-Item -LiteralPath (Join-Path $repositoryRoot 'README.md') -Destination $payloadRoot
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'LICENSE.txt') -Destination $payloadRoot
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'THIRD_PARTY.md') -Destination $payloadRoot
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'PRIVACY.md') -Destination $payloadRoot

$requiredFiles = @('MolarMap.exe', 'MolarMap.dll', 'GroundedMolar.Core.dll', 'ooz.exe',
    'Assets\grounded-marker-free-map.png', 'Assets\T_UI_MM_MorselGeneric.png',
    'README.md', 'LICENSE.txt', 'THIRD_PARTY.md', 'PRIVACY.md')
foreach ($relativePath in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $payloadRoot $relativePath) -PathType Leaf)) {
        throw "Release is incomplete: $relativePath is missing."
    }
}
if ((Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $payloadRoot 'ooz.exe')).Hash -ne $oozHash) {
    throw 'The published helper does not match the hash compiled into the application.'
}

& (Join-Path $PSScriptRoot 'Test-PublishedRuntime.ps1') -PublishDirectory $payloadRoot

$portableExecutables = @(Get-ChildItem -LiteralPath $payloadRoot -File -Recurse |
    Where-Object { $_.Extension -in @('.exe', '.dll') })
if ($signedRelease) {
    $unsignedPortableExecutables = @()
    foreach ($portableExecutable in $portableExecutables) {
        $status = (Get-AuthenticodeSignature -LiteralPath $portableExecutable.FullName).Status
        if ($status -eq [System.Management.Automation.SignatureStatus]::NotSigned) {
            $unsignedPortableExecutables += $portableExecutable
        }
        elseif ($status -ne [System.Management.Automation.SignatureStatus]::Valid) {
            throw "Refusing to replace an invalid existing signature on $($portableExecutable.FullName): $status"
        }
    }
    if ($unsignedPortableExecutables.Count -gt 0) {
        Invoke-ReleaseSignature -SignToolPath $SignToolPath -SigningMode $SigningMode `
            -Path @($unsignedPortableExecutables.FullName) -CertificateThumbprint $CertificateThumbprint `
            -CertificateStoreScope $CertificateStoreScope `
            -ArtifactSigningMetadataPath $ArtifactSigningMetadataPath -ArtifactSigningDlibPath $ArtifactSigningDlibPath `
            -TimestampUrl $TimestampUrl
        Assert-ValidAuthenticodeSignature -Path @($unsignedPortableExecutables.FullName)
    }
    foreach ($portableExecutable in $portableExecutables) {
        $status = (Get-AuthenticodeSignature -LiteralPath $portableExecutable.FullName).Status
        if ($status -ne [System.Management.Automation.SignatureStatus]::Valid) {
            throw "The MSI payload contains an untrusted PE file: $($portableExecutable.FullName) ($status)"
        }
    }
}

$wixBuildArguments = @(
    'build', (Join-Path $repositoryRoot 'installer\MolarMap.Installer.wixproj'),
    '--configuration', 'Release',
    '--configfile', $wixNugetConfigPath,
    "-p:PayloadDirectory=$payloadRoot",
    "-p:ProductVersion=$Version",
    "-p:PublisherName=$PublisherName",
    '--artifacts-path', $dotnetArtifactsRoot,
    "-p:OutputPath=$installerBuildRoot\")
if ($SkipRegressionTestsForPackagingRehearsal) { $wixBuildArguments += '-p:SuppressValidation=true' }
$dotnetExitCode = Invoke-ReleaseDotNet -RepositoryRoot $repositoryRoot -Arguments $wixBuildArguments
if ($dotnetExitCode -ne 0) { throw 'MSI build failed.' }
$builtMsi = Get-ChildItem -LiteralPath $installerBuildRoot -Filter *.msi -File -Recurse | Select-Object -First 1
if (-not $builtMsi) { throw 'WiX did not produce an MSI.' }
Copy-Item -LiteralPath $builtMsi.FullName -Destination $msiPath
if ($signedRelease) {
    Invoke-ReleaseSignature -SignToolPath $SignToolPath -SigningMode $SigningMode -Path @($msiPath) `
        -CertificateThumbprint $CertificateThumbprint -CertificateStoreScope $CertificateStoreScope `
        -ArtifactSigningMetadataPath $ArtifactSigningMetadataPath `
        -ArtifactSigningDlibPath $ArtifactSigningDlibPath -TimestampUrl $TimestampUrl
    Assert-ValidAuthenticodeSignature -Path @($msiPath)
}

if (-not $SkipRegressionTestsForPackagingRehearsal) {
    & (Join-Path $PSScriptRoot 'Test-MsiPackage.ps1') -MsiPath $msiPath -ExpectedVersion ([Version]"$Version.0")
}

$nexusMsi = Join-Path $nexusRoot (Split-Path -Leaf $msiPath)
Copy-Item -LiteralPath $msiPath -Destination $nexusMsi
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'README.md') -Destination $nexusRoot
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'LICENSE.txt') -Destination $nexusRoot
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'THIRD_PARTY.md') -Destination $nexusRoot
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'PRIVACY.md') -Destination $nexusRoot
$msiHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $msiPath).Hash
@(
    "MolarMap $Version",
    '',
    'Install: run the included signed MSI. Microsoft Store uses a separately built Store-signed MSIX for the same app version.',
    'Silent install: msiexec /i "MolarMap.msi" /qn /norestart',
    'Uninstall: Settings > Apps > Installed apps > MolarMap > Uninstall.',
    '',
    "MSI SHA-256: $msiHash"
) | Set-Content -LiteralPath (Join-Path $nexusRoot 'RELEASE_NOTES.txt') -Encoding UTF8
"$msiHash  $(Split-Path -Leaf $msiPath)" | Set-Content -LiteralPath (Join-Path $nexusRoot 'SHA256SUMS.txt') -Encoding ASCII
Compress-Archive -Path (Join-Path $nexusRoot '*') -DestinationPath $nexusArchivePath -CompressionLevel Optimal
$nexusHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $nexusArchivePath).Hash

$signatureSubject = $null
if ($signedRelease) { $signatureSubject = (Get-AuthenticodeSignature -LiteralPath $msiPath).SignerCertificate.Subject }
$manifest = [ordered]@{
    product = 'MolarMap'
    version = $Version
    architecture = 'x64'
    createdUtc = [DateTime]::UtcNow.ToString('o')
    productionReady = $signedRelease
    packagingRehearsalSkippedRegressionTests = [bool]$SkipRegressionTestsForPackagingRehearsal
    signingMode = $SigningMode
    publisher = $PublisherName
    signatureSubject = $signatureSubject
    oozSha256 = $oozHash
    installer = [ordered]@{ file = (Split-Path -Leaf $msiPath); sha256 = $msiHash }
    nexusArchive = [ordered]@{ file = (Split-Path -Leaf $nexusArchivePath); sha256 = $nexusHash }
}
$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
@(
    "$msiHash  $(Split-Path -Leaf $msiPath)",
    "$nexusHash  $(Split-Path -Leaf $nexusArchivePath)"
) | Set-Content -LiteralPath $checksumsPath -Encoding ASCII

if (-not $signedRelease) {
    @(
        'UNSIGNED DRAFT - DO NOT PUBLISH',
        'This package exists only to validate the release and installer pipeline.',
        'Run Publish-Release.ps1 with CertificateStore or ArtifactSigning for production.'
    ) | Set-Content -LiteralPath (Join-Path $releaseRoot 'DO-NOT-PUBLISH.txt') -Encoding ASCII
}

Remove-Item -LiteralPath $stagingRoot -Recurse -Force
Write-Host "Created installer: $msiPath"
Write-Host "Created Nexus archive: $nexusArchivePath"
Write-Host "Installer SHA-256: $msiHash"
Write-Host "Nexus ZIP SHA-256: $nexusHash"
if (Test-Path -LiteralPath (Join-Path $repositoryRoot "artifacts\store\$Version\store-package-manifest.json")) {
    & (Join-Path $PSScriptRoot 'Test-ChannelReleaseSync.ps1') -Version $Version
}
if (-not $signedRelease) { Write-Warning 'Unsigned draft created. It is deliberately named DO-NOT-PUBLISH.' }
