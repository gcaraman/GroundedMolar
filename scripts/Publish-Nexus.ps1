param(
    [ValidatePattern('^\d+\.\d+\.\d+([.-][0-9A-Za-z.-]+)?$')]
    [string]$Version = '1.0.0'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$artifactsRoot = Join-Path $repositoryRoot 'artifacts\nexus'
$publishRoot = Join-Path $artifactsRoot "GroundedMolar-$Version-win-x64"
$archivePath = "$publishRoot.zip"
$env:DOTNET_CLI_HOME = Join-Path $repositoryRoot '.dotnet-cli'
$env:APPDATA = Join-Path $repositoryRoot '.appdata'
$env:NUGET_PACKAGES = Join-Path $repositoryRoot '.nuget\packages'

if (Test-Path -LiteralPath $publishRoot) {
    Remove-Item -LiteralPath $publishRoot -Recurse -Force
}

dotnet publish (Join-Path $repositoryRoot 'src\GroundedMolar.App\GroundedMolar.App.csproj') `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:Version=$Version `
    -p:DebugType=None `
    -p:PublishSingleFile=false `
    --source 'https://api.nuget.org/v3/index.json' `
    --output $publishRoot
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

Copy-Item (Join-Path $repositoryRoot 'README.md') $publishRoot
Copy-Item (Join-Path $repositoryRoot 'LICENSE.txt') $publishRoot
Copy-Item (Join-Path $repositoryRoot 'THIRD_PARTY.md') $publishRoot

$requiredFiles = @(
    'GroundedMolar.exe',
    'ooz.exe',
    'Assets\grounded-marker-free-map.png',
    'Assets\T_UI_MM_MorselGeneric.png',
    'README.md',
    'LICENSE.txt',
    'THIRD_PARTY.md'
)
foreach ($relativePath in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $publishRoot $relativePath) -PathType Leaf)) {
        throw "Release is incomplete: $relativePath is missing."
    }
}

$oozHash = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $publishRoot 'ooz.exe')).Hash
if ($oozHash -ne '271D3FD02E582175FF033D0A23DCA3785B6888FA21B8CD06741BA8C19B71DF41') {
    throw "Bundled ooz.exe failed integrity validation: $oozHash"
}

& (Join-Path $PSScriptRoot 'Test-PublishedRuntime.ps1') -PublishDirectory $publishRoot
if ($LASTEXITCODE -ne 0) { throw 'Published runtime validation failed.' }

if (Test-Path -LiteralPath $archivePath) { Remove-Item -LiteralPath $archivePath }
Compress-Archive -Path (Join-Path $publishRoot '*') -DestinationPath $archivePath -CompressionLevel Optimal
$archiveHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $archivePath).Hash
Write-Host "Created $archivePath"
Write-Host "SHA-256 $archiveHash"
