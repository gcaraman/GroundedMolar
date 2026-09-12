namespace GroundedMolar.Core;

public sealed class CoordinateProjector
{
    public const double ReferenceWidth = 1258.0;
    public const double ReferenceHeight = 1258.0;
    public const double ExportedTextureSize = 4096.0;
    public const double ExportedMinBound = -100000.0;
    public const double ExportedMaxBound = 100000.0;

    // Historical screenshot calibration, retained for comparison only; not used for rendering.
    public PixelPoint WorldToReferenceMap(double worldX, double worldY) => new(629.335 + 0.00625103 * worldY, 620.711 - 0.00624265 * worldX);

    // UI_MapPanel bounds, checked against the independently registered oak-lab screenshot.
    // Zoom and pan belong to the viewport, not this world-to-map conversion.
    public MapPoint WorldToNormalizedMap(double worldX, double worldY) => new(
        (worldY - ExportedMinBound) / (ExportedMaxBound - ExportedMinBound),
        (ExportedMaxBound - worldX) / (ExportedMaxBound - ExportedMinBound));

    public PixelPoint WorldToExportedTexture(double worldX, double worldY)
    {
        var normalized = WorldToNormalizedMap(worldX, worldY);
        return new(normalized.U * ExportedTextureSize, normalized.V * ExportedTextureSize);
    }

    // Compatibility entry point for earlier diagnostic callers; these bounds are now used above.
    public PixelPoint WorldToTentativeGameBoundsTexture(double worldX, double worldY) => new(
        (worldY - ExportedMinBound) * ExportedTextureSize / (ExportedMaxBound - ExportedMinBound),
        (ExportedMaxBound - worldX) * ExportedTextureSize / (ExportedMaxBound - ExportedMinBound));
}
