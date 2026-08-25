using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using GroundedMolar.Core;

var failures = new List<string>();
var checks = 0;
Run("map zoom is bounded by fit and 16x", () =>
{
    var fit = MapZoom.FitScale(800, 600, 4096, 4096);
    Near(600d / 4096, fit, .000001);
    Near(fit, MapZoom.Clamp(0.01, fit), .000001);
    Near(16, MapZoom.Clamp(20, fit), .000001);
});
Run("map fit uses the limiting viewport axis", () =>
{
    Near(2, MapZoom.FitScale(1200, 1024, 512, 512), .000001);
    Near(1.5, MapZoom.FitScale(768, 1200, 512, 512), .000001);
});
Run("fitted map is centered on the spare axis", () =>
{
    Near(88, MapZoom.CenterOffset(1200, 1024), .000001);
    Near(0, MapZoom.CenterOffset(1024, 1200), .000001);
});
Run("fixed-size marker center stays anchored while zooming", () =>
{
    const double coordinate = 1200;
    const double markerSize = 64;
    foreach (var scale in new[] { .125, 1d, 16d })
    {
        var left = MapZoom.CenteredTopLeft(coordinate, scale, markerSize);
        Near(coordinate * scale, left + markerSize / 2, .000001);
    }
});
Run("marker coordinates stay normalized on logical pixel map", () =>
{
    Near(235.793125, MapZoom.NormalizeCoordinate(1886.345, 4096, 512), .000001);
    Near(.460533447265625, MapZoom.NormalizeCoordinate(1886.345, 4096, 1), .000001);
});
Run("unapproached marker opacity is adjustable and bounded", () =>
{
    Near(.72, MolarMarkerOpacity.Resolve(MolarApproachState.Unapproached, .72), .000001);
    Near(0, MolarMarkerOpacity.Resolve(MolarApproachState.Unapproached, -.5), .000001);
    Near(1, MolarMarkerOpacity.Resolve(MolarApproachState.Unapproached, 1.5), .000001);
    Near(1, MolarMarkerOpacity.Resolve(MolarApproachState.Approached, .2), .000001);
    Near(MolarMarkerOpacity.DefaultUnapproached, MolarMarkerOpacity.Clamp(double.NaN), .000001);
});
Run("remaining molar projection", () => { var p = new CoordinateProjector().WorldToReferenceMap(-25383.043, -7996.126); Near(579.35, p.X, .02); Near(779.17, p.Y, .02); });
Run("known calibration projection", () => { var p = new CoordinateProjector().WorldToReferenceMap(65976.484, -53238.310); Between(p.X, 294, 299); Between(p.Y, 206, 212); });
Run("accepted projection scales to exported texture", () => { var p = new CoordinateProjector().WorldToExportedTexture(-25383.043, -7996.126); Near(1886.34, p.X, .05); Near(2536.94, p.Y, .05); });
Run("tentative game bounds remain isolated", () => { var p = new CoordinateProjector().WorldToTentativeGameBoundsTexture(-25383.043, -7996.126); Near(1884.24, p.X, .05); Near(2567.84, p.Y, .05); });
Run("collected markers use the proof-of-concept pink", () =>
{
    True(MolarMarkerPalette.Collected == (255, 78, 168), $"Unexpected collected marker color {MolarMarkerPalette.Collected}.");
    True(MolarMarkerPalette.Collected != MolarMarkerPalette.Uncollected, "Collected and uncollected marker colors must be distinct.");
});
Run("marker-free map palette reproduces reference colors", () =>
{
    var background = GroundedMapPalette.Compose(1, 0, 0);
    True(background == (93, 73, 75), $"Unexpected background-mask color {background}.");
    var land = GroundedMapPalette.Compose(0, 1, 0);
    True(land == (72, 31, 25), $"Unexpected base-map color {land}.");
    var water = GroundedMapPalette.Compose(0, 0, 1);
    True(water == (31, 29, 49), $"Unexpected water color {water}.");
});
Run("exported UI bounds projection", () =>
{
    var projector = new ExportedUiBoundsProjector();
    var center = projector.WorldToTexture(0, 0);
    Near(2048, center.X, .0001); Near(2048, center.Y, .0001);
    var corners = projector.WorldToTexture(100000, -100000);
    Near(0, corners.X, .0001); Near(0, corners.Y, .0001);
});
Run("preview rejects non-validated analysis", () =>
{
    var renderer = new MapPreviewRenderer();
    var unsupported = MolarAnalysis.Unsupported("test");
    Throws<InvalidOperationException>(() => renderer.RenderSvg(unsupported, [1, 2, 3]));
});
Run("screenshot headers enforce magic dimensions frame count and bounds", () =>
{
    GroundedScreenshotValidator.Validate(BuildWpfScreenshot(png: true));
    Throws<InvalidDataException>(() => GroundedScreenshotValidator.Validate(BuildPngHeader(1024, 512)));
    Throws<InvalidDataException>(() => GroundedScreenshotValidator.Validate(BuildPngHeader(512, 512)));
    Throws<InvalidDataException>(() => GroundedScreenshotValidator.Validate(BuildPngHeader(512, 512, bitDepth: 4, colorType: 6)));
    Throws<InvalidDataException>(() => GroundedScreenshotValidator.Validate(BuildPngHeader(512, 512, colorType: 1)));
    var corruptPng = BuildWpfScreenshot(png: true);
    corruptPng[^5] ^= 0x01;
    Throws<InvalidDataException>(() => GroundedScreenshotValidator.Validate(corruptPng));
    Throws<InvalidDataException>(() => GroundedScreenshotValidator.Validate([1, 2, 3, 4]));
    var animated = BuildPngHeader(512, 512, animated: true);
    Throws<InvalidDataException>(() => GroundedScreenshotValidator.Validate(animated));
    GroundedScreenshotValidator.Validate(BuildWpfScreenshot(png: false));
    Throws<InvalidDataException>(() => GroundedScreenshotValidator.Validate(BuildJpegHeader(512, 512)));
    Throws<InvalidDataException>(() => GroundedScreenshotValidator.Validate(BuildJpegHeader(512, 1024)));
    Throws<InvalidDataException>(() => GroundedScreenshotValidator.Validate(BuildJpegHeader(512, 512).Concat(BuildJpegHeader(512, 512)).ToArray()));
    Throws<InvalidDataException>(() => GroundedScreenshotValidator.Validate(new byte[GroundedScreenshotValidator.MaximumEncodedBytes + 1]));
    WithRawFile(new byte[GroundedScreenshotValidator.MaximumEncodedBytes + 1], path =>
        Throws<InvalidDataException>(() => GroundedScreenshotValidator.ReadValidated(path)));
});
Run("validated 512x512 PNG and JPEG screenshots decode through WPF", () =>
{
    foreach (var encoded in new[] { BuildWpfScreenshot(png: true), BuildWpfScreenshot(png: false) })
    {
        GroundedScreenshotValidator.Validate(encoded);
        using var stream = new MemoryStream(encoded, writable: false);
        var decoder = System.Windows.Media.Imaging.BitmapDecoder.Create(stream,
            System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,
            System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
        True(decoder.Frames.Count == 1, "Validated screenshot decoded with more than one WPF frame.");
        True(decoder.Frames[0].PixelWidth == 512 && decoder.Frames[0].PixelHeight == 512, "Validated screenshot dimensions changed during WPF decode.");
    }
});
Run("unsupported formats fail closed", () => { var r = new ProfiledMolarAnalyzer([], new ConservativeStateResolver()).Analyze(new byte[] { 1, 2, 3 }); True(r.Confidence == SaveConfidence.Unsupported && r.Selected.Count == 0, "Unknown bytes produced markers."); });
Run("persistent actor state separates approach from collection", () =>
{
    var actorGuid = new UnrealGuid(1, 2, 3, 4);
    var spawn = new MolarSpawn(new UnrealGuid(5, 6, 7, 8), actorGuid, 10, 20, 30, "test", "test", 0, false, MolarState.Unknown, MolarApproachState.Unknown);
    var resolver = new GroundedMolarStateResolverV1();
    True(resolver.Resolve(spawn, new PersistentActorRecord(actorGuid, new byte[] { 0 })) == new MolarResolution(MolarState.Uncollected, MolarApproachState.Unapproached), "State 0 was not resolved as unapproached and uncollected.");
    True(resolver.Resolve(spawn, new PersistentActorRecord(actorGuid, new byte[] { 1 })) == new MolarResolution(MolarState.Uncollected, MolarApproachState.Approached), "State 1 was not resolved as approached and uncollected.");
    True(resolver.Resolve(spawn, null) == new MolarResolution(MolarState.Collected, MolarApproachState.Unknown), "Absent persistent actor was not resolved as collected.");
});
Run("v1 actor lookup preserves recognized and absent persistent states in one pass", () =>
{
    var passes = 0;
    var profile = new GroundedSaveFormatProfileV1(() => passes++);
    var bytes = BuildSyntheticMolarSave(includeNormalState: true, normalState: 1, includeUnderwaterState: false);
    var analysis = new ProfiledMolarAnalyzer([profile], new GroundedMolarStateResolverV1()).Analyze(bytes);
    True(analysis.Confidence == SaveConfidence.Validated, analysis.Diagnostic ?? "Synthetic save was not validated.");
    True(passes == 1, $"Expected one actor lookup pass, got {passes}.");
    True(analysis.Uncollected.Count == 1 && analysis.Uncollected.Single().ApproachState == MolarApproachState.Approached, "Recognized state 1 changed.");
    True(analysis.Collected.Count == 1, "An actor with only its selected-spawn reference was not collected.");
});
Run("v1 actor lookup rejects unrecognized persistent-state signatures", () =>
{
    var bytes = BuildSyntheticMolarSave(includeNormalState: true, normalState: 2, includeUnderwaterState: false);
    var analysis = new ProfiledMolarAnalyzer([new GroundedSaveFormatProfileV1()], new GroundedMolarStateResolverV1()).Analyze(bytes);
    True(analysis.Confidence == SaveConfidence.Unsupported && analysis.Selected.Count == 0, "An unrecognized second actor record did not fail closed.");
});
Run("v1 actor lookup rejects unsupported occurrence counts", () =>
{
    var bytes = BuildSyntheticMolarSave(includeNormalState: true, normalState: 1, includeUnderwaterState: false).ToList();
    bytes.AddRange(new UnrealGuid(5, 6, 7, 8).ToSerializedBytes());
    var analysis = new ProfiledMolarAnalyzer([new GroundedSaveFormatProfileV1()], new GroundedMolarStateResolverV1()).Analyze(bytes.ToArray());
    True(analysis.Confidence == SaveConfidence.Unsupported && analysis.Selected.Count == 0, "A third actor GUID occurrence did not fail closed.");
});
Run("actor lookup remains linear for same-prefix GUIDs and honors cancellation", () =>
{
    var actors = Enumerable.Range(0, 1024).Select(index => new UnrealGuid(checked((uint)(index * 256 + 0x7F)), 2, 3, 4)).ToArray();
    var bytes = new byte[8 * 1024 * 1024];
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    var occurrences = GroundedSaveFormatProfileV1.FindActorOccurrences(bytes, actors);
    stopwatch.Stop();
    True(occurrences.Count == actors.Length && occurrences.All(pair => pair.Value.Count == 0), "Worst-case lookup returned false occurrences.");
    True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Linear actor lookup exceeded its 5-second regression bound: {stopwatch.Elapsed}.");
    using var cancelled = new CancellationTokenSource();
    cancelled.Cancel();
    Throws<OperationCanceledException>(() => GroundedSaveFormatProfileV1.FindActorOccurrences(bytes, actors, cancelled.Token));
});
Run("v1 profile accepts underwater entry within the normal group", () =>
{
    var bytes = BuildSyntheticMixedMolarSave();
    var analysis = new ProfiledMolarAnalyzer([new GroundedSaveFormatProfileV1()], new GroundedMolarStateResolverV1()).Analyze(bytes);
    True(analysis.Confidence == SaveConfidence.Validated, analysis.Diagnostic ?? "Mixed-group save was not validated.");
    True(analysis.Selected.Count == 3, $"Expected 3 selected spawns, got {analysis.Selected.Count}.");
    True(analysis.Selected.Count(x => !x.IsUnderwater) == 1, "Normal entry count wrong in mixed-group save.");
    True(analysis.Selected.Count(x => x.IsUnderwater) == 2, "Underwater entry count wrong in mixed-group save.");
});
Run("stable analysis service reuses injected services without retaining save bytes", () =>
{
    var decoder = new CountingSaveDecoder();
    var analyzer = new CountingAnalyzer();
    var service = new SaveAnalysisService(decoder, analyzer);
    _ = service.Analyze("first");
    _ = service.Analyze("second");
    True(decoder.Calls == 2 && analyzer.Calls == 2, "Repeated analysis did not reuse the initialized service dependencies.");
    True(analyzer.Inputs.SequenceEqual(new byte[] { 1, 2 }), "Analysis inputs leaked mutable parse state across calls.");
});
Run("decoder security failures become unsupported analysis", () =>
{
    var service = new SaveAnalysisService(new ThrowingSaveDecoder(new TimeoutException("limit")), new CountingAnalyzer());
    var result = service.Analyze("ignored");
    True(result.Confidence == SaveConfidence.Unsupported && result.Selected.Count == 0, "A decoder limit failure did not fail closed as Unsupported.");
});
Run("discovered POIs are read atomically from the party discovery list", () =>
{
    var bytes = BuildPartyDiscoveryRecord("ScannerBracelet", "POIGrasslandsBaseball", "BestiaryGrub", "POIFourLeafClover");
    var analysis = new GroundedDiscoveredPoiReaderV1().Read(bytes);
    True(analysis.Confidence == SaveConfidence.Validated, analysis.Diagnostic ?? "Discovery record was not validated.");
    True(analysis.PoiIds.SequenceEqual(new[] { "POIGrasslandsBaseball", "POIFourLeafClover" }), "The authoritative POI rows changed.");
});
Run("changed party discovery signatures fail closed", () =>
{
    var bytes = BuildPartyDiscoveryRecord("POIGrasslandsBaseball");
    var table = System.Text.Encoding.ASCII.GetBytes("/Game/Blueprints/Items/Table_AllItems.Table_AllItems");
    bytes[bytes.AsSpan().IndexOf(table)] ^= 0x20;
    var analysis = new GroundedDiscoveredPoiReaderV1().Read(bytes);
    True(analysis.Confidence == SaveConfidence.Unsupported && analysis.PoiIds.Count == 0, "A changed discovery table produced map state.");
});
Run("ambiguous party discovery records fail closed", () =>
{
    var record = BuildPartyDiscoveryRecord("POIGrasslandsBaseball");
    var bytes = record.Concat(record).ToArray();
    var analysis = new GroundedDiscoveredPoiReaderV1().Read(bytes);
    True(analysis.Confidence == SaveConfidence.Unsupported && analysis.PoiIds.Count == 0, "Duplicate discovery records produced map state.");
});
Run("csav header is parsed atomically", () => WithCsav([10, 20, 30], [1, 2, 3, 4], path =>
{
    var fake = new FakeKrakenDecoder([10, 20, 30]);
    var decoded = new GroundedCsavDecoder(fake).Decode(path);
    True(decoded.SequenceEqual(new byte[] { 10, 20, 30 }), "Decoded bytes changed.");
    True(fake.Payload!.SequenceEqual(new byte[] { 1, 2, 3, 4 }), "Header bytes leaked into the Kraken payload.");
    True(fake.ExpectedSize == 3, "Decoded size was not forwarded.");
}));
Run("truncated csav is rejected before Kraken", () => WithRawFile([1, 2, 3], path => Throws<InvalidDataException>(() => new GroundedCsavDecoder(new FakeKrakenDecoder([])).Decode(path))));
Run("oversized physical csav is rejected before payload allocation or Kraken", () => WithRawFile(new byte[17], path =>
{
    var fake = new FakeKrakenDecoder([]);
    Throws<InvalidDataException>(() => new GroundedCsavDecoder(fake, maximumDecodedSize: 16, maximumCompressedSize: 8).Decode(path));
    True(fake.Payload is null, "The oversized physical file reached Kraken.");
}));
Run("oversized csav declarations fail closed", () =>
{
    WithRawFile(BuildContainer(17, 1, [1]), path => Throws<InvalidDataException>(() => new GroundedCsavDecoder(new FakeKrakenDecoder([]), maximumDecodedSize: 16, maximumCompressedSize: 8).Decode(path)));
    WithRawFile(BuildContainer(1, 9, new byte[9]), path => Throws<InvalidDataException>(() => new GroundedCsavDecoder(new FakeKrakenDecoder([]), maximumDecodedSize: 16, maximumCompressedSize: 8).Decode(path)));
});
Run("pass-through decoded saves enforce the decoded quota", () => WithRawFile(new byte[17], path =>
    Throws<InvalidDataException>(() => new PassThroughSaveDecoder(maximumBytes: 16).Decode(path))));
Run("mismatched compressed size is rejected", () => WithRawFile(BuildContainer(3, 5, [1, 2]), path => Throws<InvalidDataException>(() => new GroundedCsavDecoder(new FakeKrakenDecoder([])).Decode(path))));
Run("mismatched decoded size is rejected", () => WithCsav([1, 2, 3], [4], path => Throws<InvalidDataException>(() => new GroundedCsavDecoder(new FakeKrakenDecoder([1, 2])).Decode(path))));
Run("ooz execution uses the hash-verified byte snapshot", () => WithTemporaryDirectory(directory =>
{
    var source = FindAncestorFile("ooz.exe");
    var copy = Path.Combine(directory, "ooz.exe");
    File.Copy(source, copy);
    var decoder = new OozKrakenDecoder(copy);
    File.WriteAllBytes(copy, [0x4D, 0x5A]);
    try { decoder.Decode(new byte[] { 1 }, 1); }
    catch (InvalidDataException e) when (e.Message.Contains("ooz.exe rejected", StringComparison.Ordinal)) { return; }
    throw new Exception("The decoder did not launch the verified ooz.exe snapshot after its source path was replaced.");
}));
Run("malformed Kraken corpus stays inside the production sandbox", () =>
{
    var decoder = new OozKrakenDecoder(FindAncestorFile("ooz.exe"), timeout: TimeSpan.FromSeconds(5));
    foreach (var payload in new[]
    {
        new byte[] { 0 },
        new byte[] { 0xFF, 0xFF, 0xFF, 0xFF },
        Enumerable.Range(0, 257).Select(index => checked((byte)(index % 251))).ToArray()
    })
        ThrowsOneOf<InvalidDataException, TimeoutException>(() => decoder.Decode(payload, 1024));
});
Run("staged ooz is verified and replacement-locked during launch", () => WithTemporaryDirectory(directory =>
{
    var executable = Path.Combine(directory, "ooz.exe");
    var bytes = new byte[] { 1, 2, 3, 4 };
    File.WriteAllBytes(executable, bytes);
    var expectedHash = SHA256.HashData(bytes);
    using (OozKrakenDecoder.OpenVerifiedExecutable(executable, expectedHash))
    {
        ThrowsFileAccessDenied(() => File.WriteAllBytes(executable, new byte[] { 5 }));
        var replacement = Path.Combine(directory, "replacement.exe");
        File.WriteAllBytes(replacement, new byte[] { 6 });
        ThrowsFileAccessDenied(() => File.Move(replacement, executable, overwrite: true));
    }
    File.WriteAllBytes(executable, new byte[] { 7 });
    Throws<InvalidDataException>(() => OozKrakenDecoder.OpenVerifiedExecutable(executable, expectedHash).Dispose());
}));
if (OperatingSystem.IsWindows())
{
    Run("production sandbox confines filesystem and denies network", () => WithTemporaryDirectory(directory =>
    {
        var command = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
        var outside = Path.Combine(Path.GetTempPath(), $"GroundedMolar-escape-{Guid.NewGuid():N}.txt");
        try
        {
            _ = WindowsSandboxedProcess.Run(command, ["/d", "/c", $"echo inside>inside.txt & echo escaped>\"{outside}\""], directory,
                TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2), 64L * 1024 * 1024);
            True(!File.Exists(Path.Combine(directory, "inside.txt")), "The stdout-only sandbox wrote inside its private directory.");
            True(!File.Exists(outside), "The sandbox wrote outside its granted directory.");

            using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            var endpoint = (System.Net.IPEndPoint)listener.LocalEndpoint;
            var curl = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "curl.exe");
            var network = WindowsSandboxedProcess.Run(curl, ["--silent", "--max-time", "1", $"http://127.0.0.1:{endpoint.Port}/"], directory,
                TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2), 64L * 1024 * 1024);
            True(network.ExitCode != 0 && !listener.Pending(), "The no-capability AppContainer opened a network connection.");
        }
        finally { File.Delete(outside); }
    }));
    Run("sandbox limits children CPU wall time memory diagnostics stdout and denies disk writes", () => WithTemporaryDirectory(directory =>
    {
        var system = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var command = Path.Combine(system, "cmd.exe");
        var childScript = Path.Combine(directory, "child.cmd");
        File.WriteAllText(childScript, "@echo off\r\nstart \"\" /wait cmd.exe /d /c \"echo child>child.txt\"\r\n");
        _ = Named("child-process limit", () => WindowsSandboxedProcess.Run(command, ["/d", "/c", childScript], directory, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2), 64L * 1024 * 1024));
        True(!File.Exists(Path.Combine(directory, "child.txt")), "The active-process limit allowed a helper child process.");

        const string busyLoop = "for /L %i in (0,0,1) do @rem";
        Throws<TimeoutException>(() => WindowsSandboxedProcess.Run(command, ["/d", "/c", busyLoop], directory,
            TimeSpan.FromMilliseconds(250), TimeSpan.FromSeconds(5), 64L * 1024 * 1024));
        using (var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100)))
            Throws<OperationCanceledException>(() => WindowsSandboxedProcess.Run(command, ["/d", "/c", busyLoop], directory,
                TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), 64L * 1024 * 1024, cancellationToken: cancellation.Token));
        var powershell = Path.Combine(system, "WindowsPowerShell", "v1.0", "powershell.exe");
        var cpu = Named("CPU-time limit", () => WindowsSandboxedProcess.Run(powershell,
            ["-NoProfile", "-NonInteractive", "-Command", "while ($true) { [Math]::Sqrt(12345) | Out-Null }"], directory,
            TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(250), 64L * 1024 * 1024));
        True(cpu.ExitCode != 0, "The Job CPU-time limit did not terminate the busy helper.");

        var memory = Named("memory limit", () => WindowsSandboxedProcess.Run(powershell, ["-NoProfile", "-NonInteractive", "-Command", "$a = New-Object byte[] 134217728; Start-Sleep 1"], directory,
            TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2), 32L * 1024 * 1024));
        True(memory.ExitCode != 0, "The Job memory limit did not terminate the oversized helper.");

        var noisyScript = Path.Combine(directory, "noisy.cmd");
        File.WriteAllText(noisyScript, "@echo off\r\nfor /L %%i in (1,1,100) do echo 012345678901234567890123456789\r\n");
        var noisy = Named("diagnostic limit", () => WindowsSandboxedProcess.Run(command, ["/d", "/c", noisyScript], directory, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2), 64L * 1024 * 1024, diagnosticLimit: 1024));
        True(noisy.StandardOutput.Length <= 1024, "Sandbox diagnostics exceeded their capture cap.");

        var binary = Named("binary stdout limit", () => WindowsSandboxedProcess.Run(command, ["/d", "/c", $"echo {new string('x', 1024)}"], directory,
            TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2), 64L * 1024 * 1024, binaryStandardOutputLimit: 513));
        True(binary.BinaryStandardOutput.Length == 513, "Binary stdout capture exceeded its strict retained-byte bound.");

        var authorizedOutput = Path.Combine(directory, "authorized.bin");
        File.WriteAllBytes(authorizedOutput, []);
        _ = Named("authorized output", () => WindowsSandboxedProcess.Run(command, ["/d", "/c", "echo ok>authorized.bin"], directory,
            TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2), 64L * 1024 * 1024, writableOutputPath: authorizedOutput));
        True(File.ReadAllText(authorizedOutput).Trim() == "ok", "The explicitly authorized sandbox output was not writable.");

        var oversizedOutput = Path.Combine(directory, "oversized.bin");
        File.WriteAllBytes(oversizedOutput, []);
        var fsutil = Path.Combine(system, "fsutil.exe");
        Throws<InvalidDataException>(() => WindowsSandboxedProcess.Run(fsutil,
            ["file", "seteof", oversizedOutput, "2097152"], directory,
            TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2), 64L * 1024 * 1024,
            maximumDirectoryBytes: 1024 * 1024, writableOutputPath: oversizedOutput));
    }));
}

var fixtureDirectory = Environment.GetEnvironmentVariable("GROUNDED_FIXTURE_DIR");
if (!string.IsNullOrWhiteSpace(fixtureDirectory))
{
    Run("real Grounded World.csav matches known decompression", () =>
    {
        var csav = Path.Combine(fixtureDirectory, "World.csav");
        var expectedPath = Path.Combine(fixtureDirectory, "World_decompressed.bin");
        var ooz = Environment.GetEnvironmentVariable("GROUNDED_OOZ_PATH") ?? FindAncestorFile("ooz.exe");
        var tempRoot = Path.Combine(Path.GetTempPath(), "GroundedMolar");
        var before = Directory.Exists(tempRoot) ? Directory.GetDirectories(tempRoot).ToHashSet(StringComparer.OrdinalIgnoreCase) : [];
        var actual = new GroundedCsavDecoder(new OozKrakenDecoder(ooz)).Decode(csav);
        var expected = File.ReadAllBytes(expectedPath);
        True(actual.SequenceEqual(expected), $"Byte mismatch: actual SHA-256 {Convert.ToHexString(SHA256.HashData(actual))}, expected {Convert.ToHexString(SHA256.HashData(expected))}.");
        var after = Directory.Exists(tempRoot) ? Directory.GetDirectories(tempRoot).ToHashSet(StringComparer.OrdinalIgnoreCase) : [];
        True(after.SetEquals(before), "Decoder left a temporary working directory behind.");
        var analysis = new ProfiledMolarAnalyzer([new GroundedSaveFormatProfileV1()], new GroundedMolarStateResolverV1()).Analyze(actual);
        True(analysis.Confidence == SaveConfidence.Validated, "Known logout save was not validated.");
        True(analysis.Selected.Count == 66 && analysis.Collected.Count == 0 && analysis.Uncollected.Count == 66 && analysis.Unknown.Count == 0, "Known logout save counts changed.");
        True(analysis.Uncollected.Count(x => x.ApproachState == MolarApproachState.Unapproached) == 65 && analysis.Uncollected.Count(x => x.ApproachState == MolarApproachState.Approached) == 1, "Known logout approach-state counts changed.");
        var remaining = analysis.Uncollected.Single(x => x.ApproachState == MolarApproachState.Approached);
        True(remaining.SpawnGuid.ToString() == "A7683985-40FC4A4E-AB8B6786-B15C7B9C", "Known remaining spawn GUID changed.");
        Near(-25383.043, remaining.WorldX, .01); Near(-7996.126, remaining.WorldY, .01); Near(1306.095, remaining.WorldZ, .01);
        var corrupted = actual.ToArray();
        var marker = System.Text.Encoding.ASCII.GetBytes("SG_NG+_MilkMolars.SG_NG+_MilkMolars_C");
        var markerPosition = corrupted.AsSpan().IndexOf(marker);
        corrupted[markerPosition] ^= 0x20;
        var rejected = new ProfiledMolarAnalyzer([new GroundedSaveFormatProfileV1()], new GroundedMolarStateResolverV1()).Analyze(corrupted);
        True(rejected.Confidence == SaveConfidence.Unsupported && rejected.Selected.Count == 0, "Damaged profile marker did not fail closed.");
    });
}
else Console.WriteLine("SKIP real Grounded fixture (set GROUNDED_FIXTURE_DIR)");

var beforeDirectory = Environment.GetEnvironmentVariable("GROUNDED_BEFORE_FIXTURE_DIR");
var afterDirectory = Environment.GetEnvironmentVariable("GROUNDED_AFTER_FIXTURE_DIR");
if (!string.IsNullOrWhiteSpace(beforeDirectory) && !string.IsNullOrWhiteSpace(afterDirectory))
{
    Run("before/after molar transition is authoritative", () =>
    {
        var ooz = Environment.GetEnvironmentVariable("GROUNDED_OOZ_PATH") ?? FindAncestorFile("ooz.exe");
        var decoder = new GroundedCsavDecoder(new OozKrakenDecoder(ooz));
        var analyzer = new ProfiledMolarAnalyzer([new GroundedSaveFormatProfileV1()], new GroundedMolarStateResolverV1());
        var before = analyzer.Analyze(decoder.Decode(Path.Combine(beforeDirectory, "World.csav")));
        var after = analyzer.Analyze(decoder.Decode(Path.Combine(afterDirectory, "World.csav")));
        True(before.Confidence == SaveConfidence.Validated && after.Confidence == SaveConfidence.Validated, "Fixture confidence was not validated.");
        True(before.Selected.Count == 62 && after.Selected.Count == 62, "Selected spawn count changed.");
        True(before.Uncollected.Count == 62 && before.Collected.Count == 0 && before.Unknown.Count == 0, "Unexpected before-state counts.");
        True(before.Uncollected.Count(x => x.ApproachState == MolarApproachState.Unapproached) == 58 && before.Uncollected.Count(x => x.ApproachState == MolarApproachState.Approached) == 4, "Unexpected before approach-state counts.");
        True(after.Uncollected.Count == 61 && after.Collected.Count == 1 && after.Unknown.Count == 0, "Unexpected after-state counts.");
        True(after.Uncollected.Count(x => x.ApproachState == MolarApproachState.Unapproached) == 58 && after.Uncollected.Count(x => x.ApproachState == MolarApproachState.Approached) == 3, "Unexpected after approach-state counts.");
        var transitioned = before.Uncollected.Single(x => after.Collected.Any(y => y.SpawnGuid == x.SpawnGuid) && !after.Uncollected.Any(y => y.SpawnGuid == x.SpawnGuid));
        True(transitioned.SpawnGuid.ToString() == "DA9E1DE9-42705554-F73E408B-7E657919", "Wrong spawn transitioned.");
        Near(-35821.598, transitioned.WorldX, .01); Near(70946.258, transitioned.WorldY, .01); Near(2342.250, transitioned.WorldZ, .01);
    });
}
else Console.WriteLine("SKIP before/after transition fixture (set GROUNDED_BEFORE_FIXTURE_DIR and GROUNDED_AFTER_FIXTURE_DIR)");

var discoveryBeforeDirectory = Environment.GetEnvironmentVariable("GROUNDED_DISCOVERY_BEFORE_FIXTURE_DIR");
var discoveryAfterDirectory = Environment.GetEnvironmentVariable("GROUNDED_DISCOVERY_AFTER_FIXTURE_DIR");
if (!string.IsNullOrWhiteSpace(discoveryBeforeDirectory) && !string.IsNullOrWhiteSpace(discoveryAfterDirectory))
{
    Run("chronological world saves preserve authoritative discovered POIs", () =>
    {
        var ooz = Environment.GetEnvironmentVariable("GROUNDED_OOZ_PATH") ?? FindAncestorFile("ooz.exe");
        var decoder = new GroundedCsavDecoder(new OozKrakenDecoder(ooz));
        var reader = new GroundedDiscoveredPoiReaderV1();
        var before = reader.Read(decoder.Decode(Path.Combine(discoveryBeforeDirectory, "World.csav")));
        var after = reader.Read(decoder.Decode(Path.Combine(discoveryAfterDirectory, "World.csav")));
        True(before.Confidence == SaveConfidence.Validated && after.Confidence == SaveConfidence.Validated, "Discovery fixtures were not validated.");
        True(before.PoiIds.Count == 8 && after.PoiIds.Count == 15, "Discovery fixture counts changed.");
        True(before.PoiIds.All(after.PoiIds.Contains), "An earlier discovered POI disappeared from the later save.");
        var added = after.PoiIds.Except(before.PoiIds).Order().ToArray();
        True(added.SequenceEqual(new[]
        {
            "POICalvoCan", "POIFourLeafClover", "POIGrasslandsFieldStationRoots", "POIGrasslandsJabby",
            "POIGrasslandsLemonCrime", "POIGrasslandsWelp", "POIMilkCarton"
        }), "The chronological discovered-POI transition changed.");
    });
}
else Console.WriteLine("SKIP discovered-POI transition fixture (set GROUNDED_DISCOVERY_BEFORE_FIXTURE_DIR and GROUNDED_DISCOVERY_AFTER_FIXTURE_DIR)");

if (failures.Count > 0) { failures.ForEach(Console.Error.WriteLine); return 1; }
Console.WriteLine($"All {checks} regression checks passed."); return 0;

void Run(string name, Action check) { checks++; try { check(); Console.WriteLine($"PASS {name}"); } catch (Exception e) { failures.Add($"FAIL {name}: {e.Message}"); } }
static void Near(double expected, double actual, double tolerance) { if (Math.Abs(expected - actual) > tolerance) throw new Exception($"expected {expected} ± {tolerance}, got {actual}"); }
static void Between(double actual, double min, double max) => True(actual >= min && actual <= max, $"expected [{min}, {max}], got {actual}");
static void True(bool condition, string message) { if (!condition) throw new Exception(message); }
static T Named<T>(string name, Func<T> action) { try { return action(); } catch (Exception e) { throw new Exception($"{name}: {e.Message}", e); } }
static void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception($"Expected {typeof(T).Name}."); }
static void ThrowsOneOf<TFirst, TSecond>(Action action) where TFirst : Exception where TSecond : Exception
{
    try { action(); } catch (TFirst) { return; } catch (TSecond) { return; }
    throw new Exception($"Expected {typeof(TFirst).Name} or {typeof(TSecond).Name}.");
}
static void ThrowsFileAccessDenied(Action action)
{
    try { action(); }
    catch (IOException) { return; }
    catch (UnauthorizedAccessException) { return; }
    throw new Exception("Expected the locked file operation to be denied.");
}
static void WithTemporaryDirectory(Action<string> action)
{
    var path = Path.Combine(Path.GetTempPath(), "GroundedMolar.Tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(path);
    try { action(path); }
    finally { Directory.Delete(path, recursive: true); }
}
static byte[] BuildContainer(int decodedSize, int compressedSize, byte[] payload) { var bytes = new byte[8 + payload.Length]; BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0, 4), decodedSize); BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4, 4), compressedSize); payload.CopyTo(bytes, 8); return bytes; }
static byte[] BuildPngHeader(int width, int height, bool animated = false, byte bitDepth = 8, byte colorType = 6)
{
    using var stream = new MemoryStream();
    stream.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
    WritePngChunk(stream, "IHDR", data => { data.Write(BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(width))); data.Write(BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(height))); data.Write(new byte[] { bitDepth, colorType, 0, 0, 0 }); });
    if (animated) WritePngChunk(stream, "acTL", data => data.Write(new byte[8]));
    WritePngChunk(stream, "IEND", _ => { });
    return stream.ToArray();
}
static void WritePngChunk(Stream stream, string type, Action<MemoryStream> writeData)
{
    using var data = new MemoryStream(); writeData(data);
    stream.Write(BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(checked((int)data.Length))));
    stream.Write(System.Text.Encoding.ASCII.GetBytes(type)); data.Position = 0; data.CopyTo(stream); stream.Write(new byte[4]);
}
static byte[] BuildJpegHeader(int width, int height) =>
[
    0xFF, 0xD8,
    0xFF, 0xC0, 0x00, 0x08, 0x08,
    checked((byte)(height >> 8)), checked((byte)(height & 0xFF)), checked((byte)(width >> 8)), checked((byte)(width & 0xFF)), 0,
    0xFF, 0xD9
];
static byte[] BuildWpfScreenshot(bool png)
{
    const int size = 512;
    var pixels = new byte[size * size * 4];
    for (var index = 0; index < pixels.Length; index += 4)
    {
        pixels[index] = 0x20;
        pixels[index + 1] = 0x40;
        pixels[index + 2] = 0x80;
        pixels[index + 3] = 0xFF;
    }
    var source = System.Windows.Media.Imaging.BitmapSource.Create(size, size, 96, 96,
        System.Windows.Media.PixelFormats.Bgra32, null, pixels, size * 4);
    System.Windows.Media.Imaging.BitmapEncoder encoder = png
        ? new System.Windows.Media.Imaging.PngBitmapEncoder()
        : new System.Windows.Media.Imaging.JpegBitmapEncoder { QualityLevel = 90 };
    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(source));
    using var output = new MemoryStream();
    encoder.Save(output);
    return output.ToArray();
}
static byte[] BuildPartyDiscoveryRecord(params string[] rows)
{
    const string component = "/Script/Maine.PartyComponent";
    const string table = "/Game/Blueprints/Items/Table_AllItems.Table_AllItems";
    using var payload = new MemoryStream();
    payload.WriteByte(0);
    foreach (var row in rows) { WriteFString(payload, table); WriteFString(payload, row); }
    payload.Write(new byte[] { 0x20, 0, 0, 0, 1, 0, 0, 0 });
    using var record = new MemoryStream();
    WriteFString(record, component);
    record.Write(BitConverter.GetBytes(checked((uint)payload.Length)));
    payload.Position = 0;
    payload.CopyTo(record);
    return record.ToArray();
}
static void WriteFString(Stream stream, string value) { var bytes = System.Text.Encoding.ASCII.GetBytes(value); stream.Write(BitConverter.GetBytes(bytes.Length + 1)); stream.Write(bytes); stream.WriteByte(0); }
static byte[] BuildSyntheticMixedMolarSave()
{
    // Normal group with 2 entries: first is normal, second is underwater (mixed).
    // Underwater group with 1 entry. All actors absent (all collected).
    var spawnNormal = new UnrealGuid(1, 2, 3, 4);
    var actorNormal = new UnrealGuid(5, 6, 7, 8);
    var spawnMixed = new UnrealGuid(21, 22, 23, 24);
    var actorMixed = new UnrealGuid(25, 26, 27, 28);
    var spawnUnderwater = new UnrealGuid(9, 10, 11, 12);
    var actorUnderwater = new UnrealGuid(13, 14, 15, 16);
    using var stream = new MemoryStream();
    // Normal group: count=2, first entry (89 bytes), second entry (85 bytes last)
    var normalGroupMarker = System.Text.Encoding.ASCII.GetBytes("/Game/Blueprints/Items/SpawnPoints/SpawnGroups/SG_NG+_MilkMolars.SG_NG+_MilkMolars_C\0");
    stream.Write(normalGroupMarker); stream.Write(BitConverter.GetBytes(2)); stream.Write(new byte[4]);
    WriteFString(stream, "/Game/Blueprints/Items/SpawnPoints/SpawnData/SD_MilkMolar_NG+.SD_MilkMolar_NG+_C");
    stream.Write(MakeRecord(spawnNormal, actorNormal, isLast: false));
    WriteFString(stream, "/Game/Blueprints/Items/SpawnPoints/SpawnData/SD_MilkMolar_Underwater_NG+.SD_MilkMolar_Underwater_NG+_C");
    stream.Write(MakeRecord(spawnMixed, actorMixed, isLast: true));
    WriteMolarGroup(stream, "/Game/Blueprints/Items/SpawnPoints/SpawnGroups/SG_NG+_MilkMolarsUnderwater.SG_NG+_MilkMolarsUnderwater_C", "/Game/Blueprints/Items/SpawnPoints/SpawnData/SD_MilkMolar_Underwater_NG+.SD_MilkMolar_Underwater_NG+_C", spawnUnderwater, actorUnderwater);
    return stream.ToArray();
}
static byte[] MakeRecord(UnrealGuid spawnGuid, UnrealGuid actorGuid, bool isLast)
{
    var record = new byte[isLast ? 85 : 89];
    BitConverter.GetBytes(1f).CopyTo(record, 12); BitConverter.GetBytes(10f).CopyTo(record, 16); BitConverter.GetBytes(20f).CopyTo(record, 20); BitConverter.GetBytes(30f).CopyTo(record, 24);
    BitConverter.GetBytes(1f).CopyTo(record, 28); BitConverter.GetBytes(1f).CopyTo(record, 32); BitConverter.GetBytes(1f).CopyTo(record, 36);
    spawnGuid.ToSerializedBytes().CopyTo(record, 40); BitConverter.GetBytes(1).CopyTo(record, 56); record[60] = 1;
    actorGuid.ToSerializedBytes().CopyTo(record, 64); BitConverter.GetBytes(1).CopyTo(record, 80);
    return record;
}
static byte[] BuildSyntheticMolarSave(bool includeNormalState, byte normalState, bool includeUnderwaterState)
{
    var normalActor = new UnrealGuid(5, 6, 7, 8);
    var underwaterActor = new UnrealGuid(13, 14, 15, 16);
    using var stream = new MemoryStream();
    WriteMolarGroup(stream, "/Game/Blueprints/Items/SpawnPoints/SpawnGroups/SG_NG+_MilkMolars.SG_NG+_MilkMolars_C", "/Game/Blueprints/Items/SpawnPoints/SpawnData/SD_MilkMolar_NG+.SD_MilkMolar_NG+_C", new UnrealGuid(1, 2, 3, 4), normalActor);
    WriteMolarGroup(stream, "/Game/Blueprints/Items/SpawnPoints/SpawnGroups/SG_NG+_MilkMolarsUnderwater.SG_NG+_MilkMolarsUnderwater_C", "/Game/Blueprints/Items/SpawnPoints/SpawnData/SD_MilkMolar_Underwater_NG+.SD_MilkMolar_Underwater_NG+_C", new UnrealGuid(9, 10, 11, 12), underwaterActor);
    if (includeNormalState) WriteActorState(stream, normalActor, normalState);
    if (includeUnderwaterState) WriteActorState(stream, underwaterActor, 0);
    return stream.ToArray();
}
static void WriteMolarGroup(Stream stream, string groupAsset, string spawnData, UnrealGuid spawnGuid, UnrealGuid actorGuid)
{
    var marker = System.Text.Encoding.ASCII.GetBytes(groupAsset + "\0");
    stream.Write(marker); stream.Write(BitConverter.GetBytes(1)); stream.Write(new byte[4]); WriteFString(stream, spawnData);
    var record = new byte[85];
    BitConverter.GetBytes(1f).CopyTo(record, 12); BitConverter.GetBytes(10f).CopyTo(record, 16); BitConverter.GetBytes(20f).CopyTo(record, 20); BitConverter.GetBytes(30f).CopyTo(record, 24);
    BitConverter.GetBytes(1f).CopyTo(record, 28); BitConverter.GetBytes(1f).CopyTo(record, 32); BitConverter.GetBytes(1f).CopyTo(record, 36);
    spawnGuid.ToSerializedBytes().CopyTo(record, 40); BitConverter.GetBytes(1).CopyTo(record, 56); record[60] = 1;
    actorGuid.ToSerializedBytes().CopyTo(record, 64); BitConverter.GetBytes(1).CopyTo(record, 80);
    stream.Write(record);
}
static void WriteActorState(Stream stream, UnrealGuid actorGuid, byte state)
{
    stream.Write(actorGuid.ToSerializedBytes()); stream.Write(new byte[] { state, 0, 4, 0, 0, 0, 0, 0 });
}
static void WithCsav(byte[] decoded, byte[] payload, Action<string> action) => WithRawFile(BuildContainer(decoded.Length, payload.Length, payload), action);
static void WithRawFile(byte[] bytes, Action<string> action) { var path = Path.Combine(Path.GetTempPath(), $"GroundedMolar-test-{Guid.NewGuid():N}.csav"); try { File.WriteAllBytes(path, bytes); action(path); } finally { File.Delete(path); } }
static string FindAncestorFile(string name) { for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent) { var path = Path.Combine(directory.FullName, name); if (File.Exists(path)) return path; } throw new FileNotFoundException($"Could not locate {name}."); }

sealed class FakeKrakenDecoder(byte[] result) : IKrakenDecoder
{
    public byte[]? Payload { get; private set; }
    public int ExpectedSize { get; private set; }
    public byte[] Decode(ReadOnlyMemory<byte> compressedPayload, int expectedDecodedSize) { Payload = compressedPayload.ToArray(); ExpectedSize = expectedDecodedSize; return result; }
}
sealed class CountingSaveDecoder : ISaveDecoder
{
    public int Calls { get; private set; }
    public byte[] Decode(string filePath) => [checked((byte)++Calls)];
}
sealed class ThrowingSaveDecoder(Exception exception) : ISaveDecoder
{
    public byte[] Decode(string filePath) => throw exception;
}
sealed class CountingAnalyzer : IMolarAnalyzer
{
    public int Calls { get; private set; }
    public List<byte> Inputs { get; } = [];
    public MolarAnalysis Analyze(ReadOnlyMemory<byte> worldSave) { Calls++; Inputs.Add(worldSave.Span[0]); return MolarAnalysis.Unsupported("test"); }
}
