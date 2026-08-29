param(
    [string]$DestinationDirectory
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
if (-not $DestinationDirectory) { $DestinationDirectory = Join-Path $repositoryRoot 'store-assets' }
New-Item -ItemType Directory -Path $DestinationDirectory -Force | Out-Null

Add-Type -AssemblyName System.Drawing
$marketplaceSourcePath = Join-Path $repositoryRoot 'src\GroundedMolar.App\Assets\MolarMap-brand-v4.png'
$source = [System.Drawing.Image]::FromFile($marketplaceSourcePath)
$canvas = New-Object System.Drawing.Bitmap(300, 300, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($canvas)
try {
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.DrawImage($source, 0, 0, 300, 300)
    $destination = Join-Path $DestinationDirectory 'MolarMap-StoreLogo-300x300.png'
    $canvas.Save($destination, [System.Drawing.Imaging.ImageFormat]::Png)
    Write-Host "Created $destination"
}
finally {
    $graphics.Dispose()
    $canvas.Dispose()
    $source.Dispose()
}
