using System.Text.Json;

namespace GroundedMolar.Core;

public sealed record MolarHint(
    int Id,
    int Number,
    string Description,
    int CategoryId,
    double GuideX,
    double GuideY);

public static class MolarHintCatalog
{
    private const int ExpectedEntryCount = 219;
    private const double GuideMapSize = 1254;
    private static readonly Lazy<IReadOnlyList<MolarHint>> Entries =
        new(() => Load(DefaultPath), LazyThreadSafetyMode.ExecutionAndPublication);

    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, "Data", "milk-molar-guide.json");

    public static IReadOnlyList<MolarHint> All => Entries.Value;

    public static int Count => All.Count;

    public static MolarHint? FindClosest(MolarSpawn spawn)
    {
        var mapX = (spawn.WorldY - CoordinateProjector.ExportedMinBound) * GuideMapSize /
            (CoordinateProjector.ExportedMaxBound - CoordinateProjector.ExportedMinBound);
        var mapY = (CoordinateProjector.ExportedMaxBound - spawn.WorldX) * GuideMapSize /
            (CoordinateProjector.ExportedMaxBound - CoordinateProjector.ExportedMinBound);
        return All
            .Where(entry => (entry.CategoryId == 2) == spawn.IsUnderwater)
            .MinBy(entry => SquaredDistanceOnOrientedMap(entry, mapX, mapY));
    }

    public static IReadOnlyList<MolarHint> Load(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var document = JsonSerializer.Deserialize<MolarGuideDocument>(stream, JsonOptions)
                ?? throw new InvalidDataException("The Milk Molar guide file is empty.");
            Validate(document);
            return document.Entries!.ToArray();
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The Milk Molar guide file contains invalid JSON.", exception);
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static void Validate(MolarGuideDocument document)
    {
        if (document.SchemaVersion != 1)
        {
            throw new InvalidDataException($"Unsupported Milk Molar guide schema {document.SchemaVersion}.");
        }
        if (document.CoordinateSystem is null ||
            document.CoordinateSystem.Width != GuideMapSize ||
            document.CoordinateSystem.Height != GuideMapSize)
        {
            throw new InvalidDataException("The Milk Molar guide coordinate system must be 1254 x 1254.");
        }
        if (document.Entries is null || document.Entries.Count != ExpectedEntryCount)
        {
            throw new InvalidDataException(
                $"Expected {ExpectedEntryCount} Milk Molar guide entries, found {document.Entries?.Count ?? 0}.");
        }
        if (document.Entries.Select(entry => entry.Id).Distinct().Count() != ExpectedEntryCount)
        {
            throw new InvalidDataException("Milk Molar guide entry IDs must be unique.");
        }

        foreach (var entry in document.Entries)
        {
            if (entry.Id <= 0 || entry.Number <= 0 || entry.CategoryId is < 1 or > 3 ||
                string.IsNullOrWhiteSpace(entry.Description) ||
                !double.IsFinite(entry.GuideX) || !double.IsFinite(entry.GuideY) ||
                entry.GuideX is < 0 or > GuideMapSize || entry.GuideY is < 0 or > GuideMapSize)
            {
                throw new InvalidDataException($"Milk Molar guide entry {entry.Id} is malformed.");
            }
        }

        ValidateCategoryCount(document.Entries, categoryId: 1, expected: 126);
        ValidateCategoryCount(document.Entries, categoryId: 2, expected: 21);
        ValidateCategoryCount(document.Entries, categoryId: 3, expected: 72);
    }

    private static void ValidateCategoryCount(IReadOnlyList<MolarHint> entries, int categoryId, int expected)
    {
        var actual = entries.Count(entry => entry.CategoryId == categoryId);
        if (actual != expected)
        {
            throw new InvalidDataException(
                $"Expected {expected} Milk Molar guide entries in category {categoryId}, found {actual}.");
        }
    }

    private static double SquaredDistanceOnOrientedMap(MolarHint entry, double mapX, double mapY)
    {
        // The community guide uses the unrotated source texture. MolarMap's proven map
        // reconstruction rotates exported UI layers 90 degrees counter-clockwise.
        var orientedGuideX = entry.GuideY;
        var orientedGuideY = GuideMapSize - entry.GuideX;
        var dx = orientedGuideX - mapX;
        var dy = orientedGuideY - mapY;
        return dx * dx + dy * dy;
    }

    private sealed record MolarGuideDocument(
        int SchemaVersion,
        GuideCoordinateSystem? CoordinateSystem,
        IReadOnlyList<MolarHint>? Entries);

    private sealed record GuideCoordinateSystem(double Width, double Height);
}
