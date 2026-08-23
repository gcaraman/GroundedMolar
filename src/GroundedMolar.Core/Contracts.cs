namespace GroundedMolar.Core;

public interface ISaveSource { Stream OpenWorldSave(); DateTime GetLastWriteTime(); }
public interface ISaveDecoder { byte[] Decode(string filePath); }
public interface IKrakenDecoder { byte[] Decode(ReadOnlyMemory<byte> compressedPayload, int expectedDecodedSize); }
public interface IMolarStateResolver { MolarResolution Resolve(MolarSpawn spawn, PersistentActorRecord? actor); }
public interface ISaveFormatProfile
{
    string Name { get; }
    bool CanParse(ReadOnlySpan<byte> bytes);
    IReadOnlyList<MolarSpawn> ReadMolarSpawns(ReadOnlySpan<byte> bytes);
    IReadOnlyList<PersistentActorRecord> ReadActors(ReadOnlySpan<byte> bytes);
}
public interface IAtomicSaveFormatProfile : ISaveFormatProfile
{
    ParsedSaveFormat Parse(ReadOnlySpan<byte> bytes);
}
public sealed record ParsedSaveFormat(IReadOnlyList<MolarSpawn> Spawns, IReadOnlyList<PersistentActorRecord> Actors);
public interface IMolarAnalyzer { MolarAnalysis Analyze(ReadOnlyMemory<byte> worldSave); }
