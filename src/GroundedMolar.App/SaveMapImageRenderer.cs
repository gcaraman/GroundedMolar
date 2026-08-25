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
    private static readonly Lazy<Task<BitmapSource>> Map = new(
        () => Task.Run(LoadAndValidateMap),
        LazyThreadSafetyMode.ExecutionAndPublication);
    private static readonly Lazy<BitmapSource> MarkerIcon = new(() => LoadBitmap(Path.Combine(AppContext.BaseDirectory, "Assets", "T_UI_MM_MorselGeneric.png")), LazyThreadSafetyMode.ExecutionAndPublication);

    public static async Task<BitmapSource> LoadMapAsync(MolarAnalysis analysis, CancellationToken cancellationToken)
    {
        if (analysis.Confidence != SaveConfidence.Validated)
            throw new InvalidOperationException("Map rendering requires Validated save confidence.");
        if (analysis.Uncollected.Any(marker => marker.State != MolarState.Uncollected))
            throw new InvalidOperationException("The uncollected set contains a marker with a different state.");

        return await Map.Value.WaitAsync(cancellationToken);
    }

    private static BitmapSource LoadAndValidateMap()
    {
        var background = LoadBitmap(Path.Combine(AppContext.BaseDirectory, "Assets", "grounded-marker-free-map.png"));
        if (background.PixelWidth != LogicalMapSize || background.PixelHeight != LogicalMapSize)
            throw new InvalidDataException($"Expected a {LogicalMapSize} x {LogicalMapSize} logical map image.");
        return background;
    }

    public static BitmapSource LoadMarkerIcon() => MarkerIcon.Value;

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
