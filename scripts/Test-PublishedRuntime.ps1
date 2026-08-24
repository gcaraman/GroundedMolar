param(
    [Parameter(Mandatory)]
    [string]$PublishDirectory,

    [Version]$MinimumVersion = '10.0.11'
)

$ErrorActionPreference = 'Stop'
$publishRoot = (Resolve-Path -LiteralPath $PublishDirectory).Path
$runtimeAssemblies = @(
    'PresentationCore.dll',
    'PresentationFramework.dll',
    'WindowsBase.dll'
)

foreach ($assemblyName in $runtimeAssemblies) {
    $assemblyPath = Join-Path $publishRoot $assemblyName
    if (-not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) {
        throw "Published Windows Desktop runtime assembly is missing: $assemblyName. The release must remain self-contained and inspectable."
    }

    $versionText = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($assemblyPath).ProductVersion
    $numericVersion = ($versionText -split '[+-]', 2)[0]
    $actualVersion = [Version]$numericVersion
    if ($actualVersion -lt $MinimumVersion) {
        throw "$assemblyName is vulnerable or below the release floor: $actualVersion; required $MinimumVersion or newer."
    }
    Write-Host "Verified $assemblyName product version $actualVersion"
}
