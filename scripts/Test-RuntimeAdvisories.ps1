param(
    [string]$RuntimePackage = 'Microsoft.WindowsDesktop.App.Runtime.win-x64',
    [string]$RuntimeVersion = '10.0.11'
)

$ErrorActionPreference = 'Stop'
$headers = @{
    Accept = 'application/vnd.github+json'
    'X-GitHub-Api-Version' = '2022-11-28'
    'User-Agent' = 'GroundedMolar-runtime-advisory-check'
}
$query = [Uri]::EscapeDataString("$RuntimePackage@$RuntimeVersion")
$uri = "https://api.github.com/advisories?ecosystem=nuget&affects=$query&per_page=100"
$response = Invoke-RestMethod -Uri $uri -Headers $headers
$advisories = @($response | Where-Object { $_ -and $_.ghsa_id })
if ($advisories.Count -gt 0) {
    $summary = ($advisories | ForEach-Object { "$($_.ghsa_id): $($_.summary)" }) -join [Environment]::NewLine
    throw "The pinned Windows Desktop runtime has published security advisories:$([Environment]::NewLine)$summary"
}
Write-Host "No GitHub Advisory Database entry currently affects $RuntimePackage $RuntimeVersion."
