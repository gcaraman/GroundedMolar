param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '1.0.2'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repositoryRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'Release-Signing.ps1')
& (Join-Path $PSScriptRoot 'Test-Repo.ps1')
$releaseRoot = Join-Path $repositoryRoot "artifacts/portable/$Version"
$payload = Join-Path $releaseRoot "MolarMap-$Version"
$archive = Join-Path $releaseRoot "MolarMap-$Version.zip"
if (Test-Path -LiteralPath $releaseRoot) { throw "Release directory already exists: $releaseRoot" }
New-Item -ItemType Directory -Path $payload -Force | Out-Null
$exitCode = Invoke-ReleaseDotNet -RepositoryRoot $repositoryRoot -Arguments @(
    'publish', (Join-Path $repositoryRoot 'src/GroundedMolar.App/GroundedMolar.App.csproj'),
    '--configuration', 'Release', '--runtime', 'win-x64', '--self-contained', 'true',
    "-p:Version=$Version", "-p:ApplicationVersion=$Version.0", '-p:PublishSingleFile=false',
    '--artifacts-path', (Join-Path $releaseRoot 'build'),
    '--configfile', (Join-Path $repositoryRoot 'NuGet.Config'), '--output', $payload)
if ($exitCode -ne 0) { throw 'Portable publish failed.' }
& (Join-Path $PSScriptRoot 'Test-PublishedRuntime.ps1') -PublishDirectory $payload
foreach ($file in @('README.md', 'LICENSE.txt', 'THIRD_PARTY.md', 'PRIVACY.md')) {
    Copy-Item -LiteralPath (Join-Path $repositoryRoot $file) -Destination $payload
}
Copy-Item -LiteralPath (Join-Path $repositoryRoot "docs/RELEASE_$Version.md") -Destination (Join-Path $payload 'RELEASE_NOTES.md')
foreach ($file in @('MolarMap.exe', 'MolarMap.dll', 'ooz.exe', 'Data/milk-molar-guide.json', 'Assets/grounded-marker-free-map.png')) {
    if (-not (Test-Path -LiteralPath (Join-Path $payload $file))) { throw "Missing portable file: $file" }
}
if ((Get-FileHash (Join-Path $payload 'ooz.exe')).Hash -ne (Get-FileHash (Join-Path $repositoryRoot 'ooz.exe')).Hash) {
    throw 'Portable helper differs from the reviewed helper.'
}
Compress-Archive -LiteralPath $payload -DestinationPath $archive -CompressionLevel Optimal
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
"$hash  MolarMap-$Version.zip" | Set-Content (Join-Path $releaseRoot 'SHA256SUMS.txt') -Encoding ascii
[ordered]@{
    product = 'MolarMap'; version = $Version; architecture = 'x64'
    packageType = 'portableZip'; selfContained = $true; signing = 'unsigned'
    archive = [ordered]@{ file = "MolarMap-$Version.zip"; sha256 = $hash; bytes = (Get-Item $archive).Length }
} | ConvertTo-Json -Depth 3 | Set-Content (Join-Path $releaseRoot 'portable-release-manifest.json')
Write-Host "Created portable GitHub/Nexus archive: $archive"
Write-Host "SHA-256: $hash"
