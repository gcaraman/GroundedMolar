using System.Buffers.Binary;
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
Run("mismatched compressed size is rejected", () => WithRawFile(BuildContainer(3, 5, [1, 2]), path => Throws<InvalidDataException>(() => new GroundedCsavDecoder(new FakeKrakenDecoder([])).Decode(path))));
Run("mismatched decoded size is rejected", () => WithCsav([1, 2, 3], [4], path => Throws<InvalidDataException>(() => new GroundedCsavDecoder(new FakeKrakenDecoder([1, 2])).Decode(path))));

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
static void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception($"Expected {typeof(T).Name}."); }
static byte[] BuildContainer(int decodedSize, int compressedSize, byte[] payload) { var bytes = new byte[8 + payload.Length]; BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0, 4), decodedSize); BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4, 4), compressedSize); payload.CopyTo(bytes, 8); return bytes; }
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
sealed class CountingAnalyzer : IMolarAnalyzer
{
    public int Calls { get; private set; }
    public List<byte> Inputs { get; } = [];
    public MolarAnalysis Analyze(ReadOnlyMemory<byte> worldSave) { Calls++; Inputs.Add(worldSave.Span[0]); return MolarAnalysis.Unsupported("test"); }
}
