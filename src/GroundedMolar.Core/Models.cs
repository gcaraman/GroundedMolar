namespace GroundedMolar.Core;

public enum MolarState { Unknown, Uncollected, Collected }
public enum MolarApproachState { Unknown, Unapproached, Approached }
public enum SaveConfidence { Validated, Partial, Unsupported }
public readonly record struct UnrealGuid(uint A, uint B, uint C, uint D)
{
    public static UnrealGuid FromSerialized(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 16) throw new ArgumentException("An Unreal FGuid requires 16 bytes.", nameof(bytes));
        return new(BitConverter.ToUInt32(bytes[0..4]), BitConverter.ToUInt32(bytes[4..8]), BitConverter.ToUInt32(bytes[8..12]), BitConverter.ToUInt32(bytes[12..16]));
    }

    public byte[] ToSerializedBytes()
    {
        var bytes = new byte[16];
        BitConverter.TryWriteBytes(bytes.AsSpan(0, 4), A); BitConverter.TryWriteBytes(bytes.AsSpan(4, 4), B);
        BitConverter.TryWriteBytes(bytes.AsSpan(8, 4), C); BitConverter.TryWriteBytes(bytes.AsSpan(12, 4), D);
        return bytes;
    }

    public override string ToString() => $"{A:X8}-{B:X8}-{C:X8}-{D:X8}";
}

public sealed record MolarSpawn(UnrealGuid SpawnGuid, UnrealGuid? ActorGuid, double WorldX, double WorldY, double WorldZ, string SpawnGroup, string SpawnData, int SpawnIndex, bool IsUnderwater, MolarState State, MolarApproachState ApproachState);
public sealed record PersistentActorRecord(UnrealGuid ActorGuid, ReadOnlyMemory<byte> StateData);
public readonly record struct MolarResolution(MolarState State, MolarApproachState ApproachState);
public sealed record MolarAnalysis(IReadOnlyList<MolarSpawn> Selected, IReadOnlyList<MolarSpawn> Collected, IReadOnlyList<MolarSpawn> Uncollected, IReadOnlyList<MolarSpawn> Unknown, SaveConfidence Confidence, string? Diagnostic)
{
    public static MolarAnalysis Unsupported(string diagnostic) => new([], [], [], [], SaveConfidence.Unsupported, diagnostic);
}
public sealed record DiscoveredMapAnalysis(IReadOnlyList<string> PoiIds, SaveConfidence Confidence, string? Diagnostic)
{
    public static DiscoveredMapAnalysis Unsupported(string diagnostic) => new([], SaveConfidence.Unsupported, diagnostic);
}
public readonly record struct MapPoint(double U, double V);
public readonly record struct PixelPoint(double X, double Y);
