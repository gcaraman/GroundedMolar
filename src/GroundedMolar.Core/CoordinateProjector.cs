namespace GroundedMolar.Core;

public sealed class CoordinateProjector
{
    public const double ReferenceWidth = 1258.0;
    public const double ReferenceHeight = 1258.0;
    public const double ExportedTextureSize = 4096.0;
    public const double ExportedMinBound = -100000.0;
    public const double ExportedMaxBound = 100000.0;

    public PixelPoint WorldToReferenceMap(double worldX, double worldY) => new(629.335 + 0.00625103 * worldY, 620.711 - 0.00624265 * worldX);
    public MapPoint WorldToNormalizedMap(double worldX, double worldY) { var p = WorldToReferenceMap(worldX, worldY); return new(p.X / ReferenceWidth, p.Y / ReferenceHeight); }

    public PixelPoint WorldToExportedTexture(double worldX, double worldY)
    {
        var normalized = WorldToNormalizedMap(worldX, worldY);
        return new(normalized.U * ExportedTextureSize, normalized.V * ExportedTextureSize);
    }

    // UI_MapPanel exports these bounds, but the native /Script/Maine conversion is not exported.
    // Keep this separate and explicitly tentative until independent in-game anchors validate it.
    public PixelPoint WorldToTentativeGameBoundsTexture(double worldX, double worldY) => new(
        (worldY - ExportedMinBound) * ExportedTextureSize / (ExportedMaxBound - ExportedMinBound),
        (ExportedMaxBound - worldX) * ExportedTextureSize / (ExportedMaxBound - ExportedMinBound));
}
