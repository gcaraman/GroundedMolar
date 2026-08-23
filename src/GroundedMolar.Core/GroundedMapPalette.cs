namespace GroundedMolar.Core;

public static class GroundedMapPalette
{
    // Fitted against the marker-free in-game reference after reproducing the
    // UI_MapBGBackyard layer order and FModel texture orientation.
    public static (byte R, byte G, byte B) Compose(double backgroundAlpha, double baseLuminance, double waterAlpha)
    {
        var r = 93.13008 * backgroundAlpha + 72.49167 * baseLuminance + 30.77786 * waterAlpha;
        var g = 72.69400 * backgroundAlpha + 30.60943 * baseLuminance + 28.87580 * waterAlpha;
        var b = 75.28484 * backgroundAlpha + 25.42412 * baseLuminance + 48.96472 * waterAlpha;
        return (ToByte(r), ToByte(g), ToByte(b));
    }

    private static byte ToByte(double value) => (byte)Math.Clamp((int)Math.Round(value), 0, 255);
}
