namespace GroundedMolar.Core;

public static class MapZoom
{
    public const double Maximum = 16;

    public static double FitScale(double viewportWidth, double viewportHeight, double imageWidth, double imageHeight)
    {
        if (viewportWidth <= 0 || viewportHeight <= 0 || imageWidth <= 0 || imageHeight <= 0) return 0;
        return Math.Min(Maximum, Math.Min(viewportWidth / imageWidth, viewportHeight / imageHeight));
    }

    public static double Clamp(double scale, double fitScale) => Math.Clamp(scale, fitScale, Maximum);

    public static double CenteredTopLeft(double imageCoordinate, double scale, double elementSize) =>
        imageCoordinate * scale - elementSize / 2;

    public static double NormalizeCoordinate(double coordinate, double sourceSize, double displaySize) =>
        coordinate * displaySize / sourceSize;

    public static double CenterOffset(double viewportExtent, double contentExtent) =>
        Math.Max(0, (viewportExtent - contentExtent) / 2);
}
