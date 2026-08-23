using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GroundedMolar.Core;

namespace GroundedMolar.App;

internal static class SaveMapImageRenderer
{
    public const int ExportedMapSize = 4096;
    public const int LogicalMapSize = 512;
    private static readonly Lazy<BitmapSource> Map = new(LoadAndValidateMap, LazyThreadSafetyMode.ExecutionAndPublication);
    private static readonly Lazy<BitmapSource> MarkerIcon = new(() => LoadBitmap(Path.Combine(AppContext.BaseDirectory, "Assets", "T_UI_MM_MorselGeneric.png")), LazyThreadSafetyMode.ExecutionAndPublication);

    public static BitmapSource LoadMap(MolarAnalysis analysis)
    {
        if (analysis.Confidence != SaveConfidence.Validated)
            throw new InvalidOperationException("Map rendering requires Validated save confidence.");
        if (analysis.Uncollected.Any(marker => marker.State != MolarState.Uncollected))
            throw new InvalidOperationException("The uncollected set contains a marker with a different state.");

        return Map.Value;
    }

    private static BitmapSource LoadAndValidateMap()
    {
        var background = LoadBitmap(Path.Combine(AppContext.BaseDirectory, "Assets", "grounded-marker-free-map.png"));
        if (background.PixelWidth != ExportedMapSize || background.PixelHeight != ExportedMapSize)
            throw new InvalidDataException($"Expected a {ExportedMapSize} x {ExportedMapSize} map image.");
        return ReduceToLogicalPixels(background);
    }

    public static BitmapSource LoadMarkerIcon() => MarkerIcon.Value;

    private static BitmapSource ReduceToLogicalPixels(BitmapSource source)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var sourceStride = ExportedMapSize * 4;
        var sourcePixels = new byte[sourceStride * ExportedMapSize];
        converted.CopyPixels(sourcePixels, sourceStride, 0);
        var targetStride = LogicalMapSize * 4;
        var targetPixels = new byte[targetStride * LogicalMapSize];
        const int reduction = ExportedMapSize / LogicalMapSize;
        for (var y = 0; y < LogicalMapSize; y++)
        for (var x = 0; x < LogicalMapSize; x++)
        {
            var sourceOffset = ((y * reduction + reduction / 2) * sourceStride) + (x * reduction + reduction / 2) * 4;
            Buffer.BlockCopy(sourcePixels, sourceOffset, targetPixels, y * targetStride + x * 4, 4);
        }
        var result = BitmapSource.Create(LogicalMapSize, LogicalMapSize, 96, 96, PixelFormats.Bgra32, null, targetPixels, targetStride);
        result.Freeze();
        return result;
    }

    private static BitmapImage LoadBitmap(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Required map asset was not found.", path);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(path);
        image.EndInit();
        image.Freeze();
        return image;
    }
}
