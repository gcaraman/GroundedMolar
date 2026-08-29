param(
    [Parameter(Mandatory = $true)]
    [string]$DestinationDirectory
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$destinationRoot = [System.IO.Path]::GetFullPath($DestinationDirectory)
New-Item -ItemType Directory -Path $destinationRoot -Force | Out-Null

# Package tiles are deliberately derived from the original single-molar application icon.
# The generated marketplace artwork is listing-only and must never be used here.
$iconPath = Join-Path $repositoryRoot 'src\GroundedMolar.App\Assets\MolarMap.ico'
Add-Type -AssemblyName System.Drawing
$sourceIcon = [System.Drawing.Icon]::new($iconPath, 64, 64)
try {
    $sourceBitmap = $sourceIcon.ToBitmap()
    try {
        $targets = [ordered]@{
            'StoreLogo.png' = 50
            'Square44x44Logo.png' = 44
            'Square150x150Logo.png' = 150
        }
        foreach ($target in $targets.GetEnumerator()) {
            $size = [int]$target.Value
            $canvas = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
            $graphics = [System.Drawing.Graphics]::FromImage($canvas)
            try {
                $graphics.Clear([System.Drawing.Color]::Transparent)
                $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
                $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                $graphics.DrawImage($sourceBitmap, 0, 0, $size, $size)
                $destination = Join-Path $destinationRoot $target.Key
                $canvas.Save($destination, [System.Drawing.Imaging.ImageFormat]::Png)
                Write-Host "Created $destination from the original MolarMap.ico"
            }
            finally {
                $graphics.Dispose()
                $canvas.Dispose()
            }
        }
    }
    finally { $sourceBitmap.Dispose() }
}
finally { $sourceIcon.Dispose() }
