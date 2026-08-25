using System.Buffers.Binary;
using System.Text;

namespace GroundedMolar.Core;

public sealed class GroundedSaveFormatProfileV1(Action? actorLookupPassCompleted = null) : IAtomicSaveFormatProfile
{
    private const string NormalGroup = "SG_NG+_MilkMolars";
    private const string UnderwaterGroup = "SG_NG+_MilkMolarsUnderwater";
    private const string NormalAsset = "/Game/Blueprints/Items/SpawnPoints/SpawnGroups/SG_NG+_MilkMolars.SG_NG+_MilkMolars_C";
    private const string UnderwaterAsset = "/Game/Blueprints/Items/SpawnPoints/SpawnGroups/SG_NG+_MilkMolarsUnderwater.SG_NG+_MilkMolarsUnderwater_C";
    private const string NormalSpawnData = "/Game/Blueprints/Items/SpawnPoints/SpawnData/SD_MilkMolar_NG+.SD_MilkMolar_NG+_C";
    private const string UnderwaterSpawnData = "/Game/Blueprints/Items/SpawnPoints/SpawnData/SD_MilkMolar_Underwater_NG+.SD_MilkMolar_Underwater_NG+_C";
    private const int RecordBytesAfterSpawnData = 89;

    public string Name => "Grounded World profile v1 (NG+ spawn records)";

    public bool CanParse(ReadOnlySpan<byte> bytes)
    {
        try
        {
            _ = Parse(bytes);
            return true;
        }
        catch (InvalidDataException) { return false; }
        catch (ArgumentOutOfRangeException) { return false; }
    }

    public IReadOnlyList<MolarSpawn> ReadMolarSpawns(ReadOnlySpan<byte> bytes) =>
        ReadGroup(bytes, NormalAsset, NormalGroup).Concat(ReadGroup(bytes, UnderwaterAsset, UnderwaterGroup)).ToArray();

    public ParsedSaveFormat Parse(ReadOnlySpan<byte> bytes) => Parse(bytes, CancellationToken.None);

    public ParsedSaveFormat Parse(ReadOnlySpan<byte> bytes, CancellationToken cancellationToken)
    {
        var spawns = ReadMolarSpawns(bytes);
        if (spawns.Count == 0 || !spawns.Any(x => !x.IsUnderwater) || !spawns.Any(x => x.IsUnderwater))
            throw new InvalidDataException("Both selected molar groups are required.");
        if (spawns.Select(x => x.SpawnGuid).Distinct().Count() != spawns.Count || spawns.Select(x => x.ActorGuid).Distinct().Count() != spawns.Count)
            throw new InvalidDataException("Selected spawn and actor GUIDs must be unique.");
        return new(spawns, ReadActors(bytes, spawns, cancellationToken));
    }

    public IReadOnlyList<PersistentActorRecord> ReadActors(ReadOnlySpan<byte> bytes)
        => ReadActors(bytes, ReadMolarSpawns(bytes), CancellationToken.None);

    private IReadOnlyList<PersistentActorRecord> ReadActors(ReadOnlySpan<byte> bytes, IReadOnlyList<MolarSpawn> spawns, CancellationToken cancellationToken)
    {
        var actors = new List<PersistentActorRecord>();
        var selected = spawns.Where(x => x.ActorGuid is not null).Select(x => x.ActorGuid!.Value).ToArray();
        var occurrences = FindActorOccurrences(bytes, selected, cancellationToken);
        actorLookupPassCompleted?.Invoke();
        foreach (var spawn in spawns)
        {
            if (spawn.ActorGuid is not { } actorGuid) continue;
            var rawMatches = occurrences[actorGuid];
            if (rawMatches.Count is < 1 or > 2) throw new InvalidDataException($"Actor {actorGuid} has an unsupported reference count ({rawMatches.Count}).");
            var matches = new List<int>();
            foreach (var position in rawMatches)
                if (IsPersistentActorSignature(bytes, position)) matches.Add(position);
            if (matches.Count > 1) throw new InvalidDataException($"Actor {actorGuid} has multiple persistent-state records.");
            if (rawMatches.Count == 2 && matches.Count == 0) throw new InvalidDataException($"Actor {actorGuid} has an unrecognized persistent-state record.");
            if (matches.Count == 1) actors.Add(new(actorGuid, new byte[] { bytes[matches[0] + 16] }));
        }
        return actors;
    }

    internal static Dictionary<UnrealGuid, List<int>> FindActorOccurrences(ReadOnlySpan<byte> bytes, IReadOnlyList<UnrealGuid> actorGuids, CancellationToken cancellationToken = default)
    {
        var result = actorGuids.ToDictionary(guid => guid, _ => new List<int>());
        for (var position = 0; position <= bytes.Length - 16; position++)
        {
            if ((position & 0xFFF) == 0) cancellationToken.ThrowIfCancellationRequested();
            var candidate = UnrealGuid.FromSerialized(bytes.Slice(position, 16));
            if (result.TryGetValue(candidate, out var positions)) positions.Add(position);
        }
        return result;
    }

    private static IReadOnlyList<MolarSpawn> ReadGroup(ReadOnlySpan<byte> bytes, string groupAsset, string groupName)
    {
        var marker = Encoding.ASCII.GetBytes(groupAsset + "\0");
        var markerPositions = FindAll(bytes, marker).ToArray();
        if (markerPositions.Length != 1) throw new InvalidDataException($"Expected one {groupName} group record, found {markerPositions.Length}.");
        var cursor = markerPositions[0] + marker.Length;
        EnsureAvailable(bytes, cursor, 8);
        var count = BinaryPrimitives.ReadInt32LittleEndian(bytes[cursor..]); cursor += 4;
        var reserved = BinaryPrimitives.ReadInt32LittleEndian(bytes[cursor..]); cursor += 4;
        if (count is < 1 or > 512 || reserved != 0) throw new InvalidDataException($"Invalid {groupName} entry header.");

        var result = new List<MolarSpawn>(count);
        for (var index = 0; index < count; index++)
        {
            var spawnData = ReadAsciiFString(bytes, ref cursor);
            // Underwater entries may appear in either group; classify per-entry rather than per-group.
            bool isUnderwaterEntry;
            if (spawnData.Equals(NormalSpawnData, StringComparison.Ordinal)) isUnderwaterEntry = false;
            else if (spawnData.Equals(UnderwaterSpawnData, StringComparison.Ordinal)) isUnderwaterEntry = true;
            else throw new InvalidDataException($"Unexpected spawn data in {groupName}[{index}].");
            var recordLength = index == count - 1 ? 85 : RecordBytesAfterSpawnData;
            EnsureAvailable(bytes, cursor, recordLength);
            var record = bytes.Slice(cursor, recordLength);
            ValidateRecord(record, groupName, index, index == count - 1);
            var x = ReadSingle(record, 16); var y = ReadSingle(record, 20); var z = ReadSingle(record, 24);
            var spawnGuid = UnrealGuid.FromSerialized(record[40..56]);
            var actorGuid = UnrealGuid.FromSerialized(record[64..80]);
            result.Add(new(spawnGuid, actorGuid, x, y, z, groupName, spawnData, index, isUnderwaterEntry, MolarState.Unknown, MolarApproachState.Unknown));
            cursor += recordLength;
        }
        return result;
    }

    private static void ValidateRecord(ReadOnlySpan<byte> record, string group, int index, bool isLast)
    {
        for (var offset = 0; offset < 40; offset += 4)
            if (!float.IsFinite(ReadSingle(record, offset))) throw new InvalidDataException($"Non-finite transform in {group}[{index}].");
        if (BinaryPrimitives.ReadInt32LittleEndian(record[56..]) != 1 || record[60] != 1 || record[61] != 0 || record[62] != 0 || record[63] != 0 || BinaryPrimitives.ReadInt32LittleEndian(record[80..]) != 1 || record[84] != 0 || (!isLast && !record[85..89].SequenceEqual(new byte[4])))
            throw new InvalidDataException($"Unsupported selected-spawn record layout in {group}[{index}].");
    }

    private static bool IsPersistentActorSignature(ReadOnlySpan<byte> bytes, int position) =>
        position + 24 <= bytes.Length && bytes[position + 16] is 0 or 1 && bytes[position + 17] == 0 && bytes[position + 18] == 4 && bytes[position + 19] == 0 && bytes[position + 20] == 0 && bytes[position + 21] == 0;

    private static float ReadSingle(ReadOnlySpan<byte> bytes, int offset) => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bytes[offset..]));

    private static string ReadAsciiFString(ReadOnlySpan<byte> bytes, ref int cursor)
    {
        EnsureAvailable(bytes, cursor, 4);
        var length = BinaryPrimitives.ReadInt32LittleEndian(bytes[cursor..]); cursor += 4;
        if (length is < 2 or > 4096) throw new InvalidDataException("Invalid spawn-data FString length.");
        EnsureAvailable(bytes, cursor, length);
        if (bytes[cursor + length - 1] != 0) throw new InvalidDataException("Spawn-data FString is not null terminated.");
        var value = Encoding.ASCII.GetString(bytes.Slice(cursor, length - 1)); cursor += length;
        return value;
    }

    private static IEnumerable<int> FindAll(ReadOnlySpan<byte> bytes, ReadOnlySpan<byte> needle)
    {
        var positions = new List<int>();
        for (var offset = 0; offset <= bytes.Length - needle.Length;)
        {
            var relative = bytes[offset..].IndexOf(needle);
            if (relative < 0) break;
            var position = offset + relative; positions.Add(position); offset = position + 1;
        }
        return positions;
    }

    private static void EnsureAvailable(ReadOnlySpan<byte> bytes, int offset, int count)
    {
        if (offset < 0 || count < 0 || offset > bytes.Length - count) throw new InvalidDataException("Unexpected end of Grounded world data.");
    }
}

public sealed class GroundedMolarStateResolverV1 : IMolarStateResolver
{
    public MolarResolution Resolve(MolarSpawn spawn, PersistentActorRecord? actor)
    {
        if (spawn.ActorGuid is null) return new(MolarState.Unknown, MolarApproachState.Unknown);
        if (actor is null) return new(MolarState.Collected, MolarApproachState.Unknown);
        if (actor.StateData.Length != 1) return new(MolarState.Unknown, MolarApproachState.Unknown);
        return actor.StateData.Span[0] switch
        {
            0 => new(MolarState.Uncollected, MolarApproachState.Unapproached),
            1 => new(MolarState.Uncollected, MolarApproachState.Approached),
            _ => new(MolarState.Unknown, MolarApproachState.Unknown)
        };
    }
}
