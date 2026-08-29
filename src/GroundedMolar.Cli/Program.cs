using GroundedMolar.Core;

if (args.Length is < 1 or > 5)
{
    PrintUsage();
    return 2;
}

string? oozArgument = null;
string? decodedOutputArgument = null;
for (var index = 1; index < args.Length; index += 2)
{
    if (index + 1 >= args.Length) { PrintUsage(); return 2; }
    if (args[index].Equals("--ooz", StringComparison.OrdinalIgnoreCase)) oozArgument = args[index + 1];
    else if (args[index].Equals("--decoded-output", StringComparison.OrdinalIgnoreCase)) decodedOutputArgument = args[index + 1];
    else { PrintUsage(); return 2; }
}

try
{
    var savePath = Path.GetFullPath(args[0]);
    var oozPath = oozArgument is not null ? Path.GetFullPath(oozArgument) : Path.Combine(AppContext.BaseDirectory, "ooz.exe");
    ISaveDecoder decoder = Path.GetExtension(savePath).Equals(".csav", StringComparison.OrdinalIgnoreCase)
        ? new GroundedCsavDecoder(new OozKrakenDecoder(oozPath))
        : new PassThroughSaveDecoder();
    var decoded = decoder.Decode(savePath);
    Console.WriteLine($"Decoded:     {decoded.Length:N0} bytes");
    if (decodedOutputArgument is not null)
    {
        var decodedOutput = Path.GetFullPath(decodedOutputArgument);
        File.WriteAllBytes(decodedOutput, decoded);
        Console.WriteLine($"Decoded file: {decodedOutput}");
    }

    var discoveredMap = new GroundedDiscoveredPoiReaderV1().Read(decoded);
    Console.WriteLine($"Map state:   {discoveredMap.Confidence}");
    Console.WriteLine($"POIs found:  {discoveredMap.PoiIds.Count}");
    if (discoveredMap.Confidence == SaveConfidence.Validated)
        foreach (var poiId in discoveredMap.PoiIds) Console.WriteLine($"Discovered:  {poiId}");
    else Console.WriteLine($"Map status:  {discoveredMap.Diagnostic}");

    IMolarAnalyzer analyzer = new ProfiledMolarAnalyzer([new GroundedSaveFormatProfileV1()], new GroundedMolarStateResolverV1());
    var analysis = analyzer.Analyze(decoded);
    Console.WriteLine($"Confidence:  {analysis.Confidence}");
    Console.WriteLine($"Selected:    {analysis.Selected.Count}");
    Console.WriteLine($"Collected:   {analysis.Collected.Count}");
    Console.WriteLine($"Uncollected: {analysis.Uncollected.Count}");
    Console.WriteLine($"Unknown:     {analysis.Unknown.Count}");
    Console.WriteLine($"Status:      {analysis.Diagnostic}");
    foreach (var molar in analysis.Uncollected)
        Console.WriteLine($"{molar.SpawnGuid}  {molar.State,-11}  ({molar.WorldX:F3}, {molar.WorldY:F3}, {molar.WorldZ:F3})  {(molar.IsUnderwater ? "Underwater" : "Normal")}");
    return analysis.Confidence == SaveConfidence.Unsupported ? 3 : 0;
}
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or TimeoutException)
{
    Console.Error.WriteLine($"Could not decode save: {exception.Message}");
    return 1;
}

static void PrintUsage() => Console.Error.WriteLine(
    "Usage: MolarMap.Cli <World.csav|decompressed.bin> [--ooz <path-to-ooz.exe>] [--decoded-output <path>]");
