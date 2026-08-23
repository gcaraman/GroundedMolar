using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GroundedMolar.Core;

if (args.Length == 3 && args[0].Equals("--build-map", StringComparison.OrdinalIgnoreCase))
{
    try
    {
        var mapIconsDirectory = Path.GetFullPath(args[1]);
        var outputPath = Path.GetFullPath(args[2]);
        GroundedMapComposer.Build(mapIconsDirectory, outputPath);
        Console.WriteLine($"Built 4096 x 4096 marker-free map at {outputPath}");
        return 0;
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
    {
        Console.Error.WriteLine($"Could not build map: {exception.Message}");
        return 1;
    }
}

if (args.Length == 3 && args[0].Equals("--collected-poc", StringComparison.OrdinalIgnoreCase))
{
    try
    {
        var mapPath = Path.GetFullPath(args[1]);
        var outputPath = Path.GetFullPath(args[2]);
        var collected = new MolarSpawn(
            new UnrealGuid(0xDA9E1DE9, 0x42705554, 0xF73E408B, 0x7E657919),
            new UnrealGuid(0x01C60192, 0x436E4C33, 0x92BFFDB9, 0xB27E65EE),
            -35821.598, 70946.258, 2342.250,
            "SG_NG+_MilkMolars", "validated transition fixture", 0, false,
            MolarState.Collected, MolarApproachState.Unknown);
        Render(mapPath, outputPath, [], [collected], null);
        Console.WriteLine($"Rendered validated collected transition {collected.SpawnGuid} in pink at ({collected.WorldX:F3}, {collected.WorldY:F3}, {collected.WorldZ:F3}).");
        return 0;
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
    {
        Console.Error.WriteLine($"Could not render collected POC: {exception.Message}");
        return 1;
    }
}

if (args.Length < 3 || (args.Length - 3) % 2 != 0)
{
    PrintUsage();
    return 2;
}

try
{
    var savePath = Path.GetFullPath(args[0]);
    var mapPath = Path.GetFullPath(args[1]);
    var outputPath = Path.GetFullPath(args[2]);
    var oozPath = Path.Combine(AppContext.BaseDirectory, "ooz.exe");
    string? iconPath = null;
    var showCollected = false;
    for (var index = 3; index < args.Length; index += 2)
    {
        if (args[index].Equals("--ooz", StringComparison.OrdinalIgnoreCase)) oozPath = Path.GetFullPath(args[index + 1]);
        else if (args[index].Equals("--icon", StringComparison.OrdinalIgnoreCase)) iconPath = Path.GetFullPath(args[index + 1]);
        else if (args[index].Equals("--show-collected", StringComparison.OrdinalIgnoreCase) && bool.TryParse(args[index + 1], out var enabled)) showCollected = enabled;
        else { PrintUsage(); return 2; }
    }
    ISaveDecoder decoder = Path.GetExtension(savePath).Equals(".csav", StringComparison.OrdinalIgnoreCase)
        ? new GroundedCsavDecoder(new OozKrakenDecoder(oozPath))
        : new PassThroughSaveDecoder();
    var decoded = decoder.Decode(savePath);
    var profile = new GroundedSaveFormatProfileV1();
    IMolarAnalyzer analyzer = new ProfiledMolarAnalyzer([profile], new GroundedMolarStateResolverV1());
    var analysis = analyzer.Analyze(decoded);
    if (analysis.Confidence != SaveConfidence.Validated)
    {
        Console.Error.WriteLine($"Preview refused: analysis confidence is {analysis.Confidence}. {analysis.Diagnostic}");
        return 3;
    }

    Render(mapPath, outputPath, analysis.Uncollected, showCollected ? analysis.Collected : [], iconPath);
    Console.WriteLine($"Rendered {analysis.Uncollected.Count} authoritative uncollected marker(s): {analysis.Uncollected.Count(marker => marker.ApproachState == MolarApproachState.Unapproached)} unapproached and {analysis.Uncollected.Count(marker => marker.ApproachState == MolarApproachState.Approached)} approached; collected shown: {(showCollected ? analysis.Collected.Count : 0)}.");
    return 0;
}
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or TimeoutException)
{
    Console.Error.WriteLine($"Could not render preview: {exception.Message}");
    return 1;
}

static void Render(string mapPath, string outputPath, IReadOnlyList<MolarSpawn> uncollected, IReadOnlyList<MolarSpawn> collected, string? iconPath)
{
    const int size = 4096;
    var background = new BitmapImage();
    background.BeginInit();
    background.CacheOption = BitmapCacheOption.OnLoad;
    background.UriSource = new Uri(mapPath);
    background.EndInit();
    background.Freeze();
    if (background.PixelWidth != size || background.PixelHeight != size)
        throw new InvalidDataException($"Expected a 4096 x 4096 map texture, got {background.PixelWidth} x {background.PixelHeight}.");

    BitmapImage? icon = null;
    if (iconPath is not null)
    {
        icon = new BitmapImage();
        icon.BeginInit();
        icon.CacheOption = BitmapCacheOption.OnLoad;
        icon.UriSource = new Uri(iconPath);
        icon.EndInit();
        icon.Freeze();
        if (icon.PixelWidth != 64 || icon.PixelHeight != 64)
            throw new InvalidDataException($"Expected a 64 x 64 Milk Molar map icon, got {icon.PixelWidth} x {icon.PixelHeight}.");
    }

    var projector = new CoordinateProjector();
    var visual = new DrawingVisual();
    using (var drawing = visual.RenderOpen())
    {
        drawing.DrawImage(background, new Rect(0, 0, size, size));
        foreach (var marker in uncollected)
        {
            var point = projector.WorldToExportedTexture(marker.WorldX, marker.WorldY);
            if (point.X is < 0 or > size || point.Y is < 0 or > size) continue;
            var center = new Point(point.X, point.Y);
            drawing.PushOpacity(marker.ApproachState == MolarApproachState.Unapproached ? 0.45 : 1.0);
            if (icon is null)
            {
                var color = MolarMarkerPalette.Uncollected;
                drawing.DrawEllipse(new SolidColorBrush(Color.FromArgb(225, color.Red, color.Green, color.Blue)), new Pen(Brushes.Black, 8), center, 30, 30);
                drawing.DrawEllipse(Brushes.White, null, center, 10, 10);
            }
            else
            {
                var iconRect = new Rect(center.X - 32, center.Y - 32, 64, 64);
                drawing.PushOpacityMask(new ImageBrush(icon));
                drawing.DrawRectangle(Brushes.White, null, iconRect);
                drawing.Pop();
            }
            drawing.Pop();
        }
        foreach (var marker in collected)
        {
            var point = projector.WorldToExportedTexture(marker.WorldX, marker.WorldY);
            if (point.X is < 0 or > size || point.Y is < 0 or > size) continue;
            var center = new Point(point.X, point.Y);
            var color = MolarMarkerPalette.Collected;
            var pink = new SolidColorBrush(Color.FromRgb(color.Red, color.Green, color.Blue));
            if (icon is null)
            {
                drawing.DrawEllipse(pink, new Pen(Brushes.White, 8), center, 34, 34);
                drawing.DrawEllipse(Brushes.White, null, center, 10, 10);
            }
            else
            {
                var iconRect = new Rect(center.X - 32, center.Y - 32, 64, 64);
                drawing.PushOpacityMask(new ImageBrush(icon));
                drawing.DrawRectangle(pink, null, iconRect);
                drawing.Pop();
            }
        }
    }

    var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
    bitmap.Render(visual);
    var encoder = new PngBitmapEncoder();
    encoder.Frames.Add(BitmapFrame.Create(bitmap));
    Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
    using var stream = File.Create(outputPath);
    encoder.Save(stream);
}

static void PrintUsage() => Console.Error.WriteLine(
    "Usage:\n  GroundedMolar.Preview --build-map <MapIcons-directory> <output.png>\n  GroundedMolar.Preview --collected-poc <map.png> <output.png>\n  GroundedMolar.Preview <World.csav|decompressed.bin> <map.png> <output.png> [--ooz <ooz.exe>] [--icon <T_UI_MM_MorselGeneric.png>] [--show-collected <true|false>]");
