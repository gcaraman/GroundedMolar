param(
    [Parameter(Mandatory)] [string]$SaveFolder,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../artifacts/hint-audit'),
    [string]$CatalogPath = (Join-Path $PSScriptRoot '../src/GroundedMolar.App/Data/milk-molar-guide.json'),
    [string]$BinaryDirectory = (Join-Path $PSScriptRoot '../artifacts/verification/bin/GroundedMolar.Cli/release')
)

# Build with Test-Repo.ps1 first. This audit never changes the catalog or saves.
# Reports contain private save-derived coordinates; keep them out of version control.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$BinaryDirectory = [IO.Path]::GetFullPath($BinaryDirectory)
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
Add-Type -Path (Join-Path $BinaryDirectory 'GroundedMolar.Core.dll')
$catalog = [GroundedMolar.Core.MolarHintCatalog]::Load([IO.Path]::GetFullPath($CatalogPath))
$decoder = [GroundedMolar.Core.GroundedCsavDecoder]::new(
    [GroundedMolar.Core.OozKrakenDecoder]::new((Join-Path $BinaryDirectory 'ooz.exe')))
$analyzer = [GroundedMolar.Core.ProfiledMolarAnalyzer]::new(
    [GroundedMolar.Core.ISaveFormatProfile[]]@([GroundedMolar.Core.GroundedSaveFormatProfileV1]::new()),
    [GroundedMolar.Core.GroundedMolarStateResolverV1]::new())
$spawns = @{}
$observations = @{}
$worlds = [Collections.Generic.HashSet[string]]::new()
$conflicts = [Collections.Generic.HashSet[string]]::new()
$counts = @{ Validated = 0; Unsupported = 0; DecodeError = 0 }
$saves = @(Get-ChildItem -LiteralPath $SaveFolder -Filter World.csav -Recurse -File | Sort-Object FullName)
foreach ($save in $saves) {
    try { $analysis = $analyzer.Analyze($decoder.Decode($save.FullName)) }
    catch { $counts.DecodeError++; continue }
    if ($analysis.Confidence -ne [GroundedMolar.Core.SaveConfidence]::Validated) {
        $counts.Unsupported++; continue
    }
    $counts.Validated++
    $world = [regex]::Match($save.Directory.Name, 'ID-([A-Fa-f0-9]+)').Groups[1].Value
    if ($world) { [void]$worlds.Add($world) }
    foreach ($spawn in $analysis.Selected) {
        $guid = $spawn.SpawnGuid.ToString()
        if ($spawns.ContainsKey($guid)) {
            $old = $spawns[$guid]
            if ($old.WorldX -ne $spawn.WorldX -or $old.WorldY -ne $spawn.WorldY -or
                $old.WorldZ -ne $spawn.WorldZ -or $old.IsUnderwater -ne $spawn.IsUnderwater) {
                [void]$conflicts.Add($guid)
            }
            $observations[$guid]++
        } else {
            $spawns[$guid] = $spawn
            $observations[$guid] = 1
        }
    }
}

function Get-GuideDistance($entry, $spawn) {
    $guideX = ($spawn.WorldX + 100000) * 1254 / 200000
    $guideY = ($spawn.WorldY + 100000) * 1254 / 200000
    return [Math]::Sqrt([Math]::Pow($entry.GuideX - $guideX, 2) + [Math]::Pow($entry.GuideY - $guideY, 2))
}

$matches = @(foreach ($guid in $spawns.Keys | Sort-Object) {
    $spawn = $spawns[$guid]
    $ranked = @($catalog | Where-Object { ($_.CategoryId -eq 2) -eq $spawn.IsUnderwater } |
        Sort-Object { Get-GuideDistance $_ $spawn })
    $hint = if ($conflicts.Contains($guid)) { $null } else {
        [GroundedMolar.Core.MolarHintCatalog]::FindClosest($spawn, $catalog)
    }
    $distance = Get-GuideDistance $ranked[0] $spawn
    $secondDistance = Get-GuideDistance $ranked[1] $spawn
    $bound = $null -ne $hint -and $hint.MatchedSpawnGuid -eq $guid
    [pscustomobject]@{
        Guid = $guid; WorldX = $spawn.WorldX; WorldY = $spawn.WorldY; WorldZ = $spawn.WorldZ
        Underwater = $spawn.IsUnderwater; Observations = $observations[$guid]
        HintId = $(if ($hint) { $hint.Id } else { $null })
        Number = $(if ($hint) { $hint.Number } else { $null })
        ReviewedBinding = $bound; PositionConflict = $conflicts.Contains($guid)
        NearestNumber = $ranked[0].Number; NearestDistance = $distance
        SecondNumber = $ranked[1].Number; SecondDistance = $secondDistance
        NeedsSpatialReview = (-not $bound -and ($distance -gt 15 -or $secondDistance - $distance -lt 2))
    }
})
$collisions = @($matches | Where-Object { $null -ne $_.HintId } | Group-Object HintId | Where-Object Count -gt 1)
$collisionIds = @($collisions | ForEach-Object { [int]$_.Name })
$entryRows = @(foreach ($entry in $catalog | Sort-Object Number) {
    $assigned = @($matches | Where-Object HintId -eq $entry.Id)
    $status = if ($entry.Id -in $collisionIds) { 'Collision - needs review' }
        elseif ($assigned.Count -eq 0) { 'No runtime match observed' }
        elseif ($assigned[0].ReviewedBinding) { 'Reviewed GUID binding' }
        elseif ($assigned[0].NeedsSpatialReview) { 'Approximate - needs spatial review' }
        else { 'Approximate - not independently verified' }
    [pscustomobject]@{
        Id = $entry.Id; Number = $entry.Number; CategoryId = $entry.CategoryId
        GuideX = $entry.GuideX; GuideY = $entry.GuideY; Description = $entry.Description
        Status = $status; MatchedSpawnCount = $assigned.Count
        SpawnGuids = (@($assigned | ForEach-Object Guid) -join ';')
    }
})
$summary = [ordered]@{
    SaveFiles = $saves.Count; ValidatedSaves = $counts.Validated
    UnsupportedSaves = $counts.Unsupported; DecodeErrors = $counts.DecodeError
    WorldsInValidatedSaves = $worlds.Count; DistinctSelectedSpawns = $spawns.Count
    ConflictingSpawnGuids = $conflicts.Count; CatalogEntries = $catalog.Count
    ReviewedBindingsObserved = @($matches | Where-Object ReviewedBinding).Count
    CollisionGuideNumbers = @($collisions | ForEach-Object { $_.Group[0].Number } | Sort-Object)
    EntryStatuses = @($entryRows | Group-Object Status | Select-Object Name, Count)
}
[void][IO.Directory]::CreateDirectory($OutputDirectory)
$matches | Export-Csv -LiteralPath (Join-Path $OutputDirectory 'spawn-matches.csv') -NoTypeInformation
$entryRows | Export-Csv -LiteralPath (Join-Path $OutputDirectory 'catalog-audit.csv') -NoTypeInformation
$summary | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'summary.json')
$summary | ConvertTo-Json -Depth 6
