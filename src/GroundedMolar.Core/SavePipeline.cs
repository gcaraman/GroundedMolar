namespace GroundedMolar.Core;

public sealed class PassThroughSaveDecoder : ISaveDecoder { public byte[] Decode(string filePath) => File.ReadAllBytes(filePath); }
public sealed class ProfiledMolarAnalyzer(IEnumerable<ISaveFormatProfile> profiles, IMolarStateResolver stateResolver) : IMolarAnalyzer
{
    private readonly ISaveFormatProfile[] _profiles = profiles.ToArray();
    public MolarAnalysis Analyze(ReadOnlyMemory<byte> worldSave)
    {
        ISaveFormatProfile? profile = null;
        ParsedSaveFormat? parsed = null;
        foreach (var candidate in _profiles)
        {
            if (candidate is IAtomicSaveFormatProfile atomic)
            {
                try { parsed = atomic.Parse(worldSave.Span); profile = candidate; break; }
                catch (InvalidDataException) { }
                catch (ArgumentOutOfRangeException) { }
            }
            else if (candidate.CanParse(worldSave.Span)) { profile = candidate; break; }
        }
        if (profile is null) return MolarAnalysis.Unsupported("No validated save-format profile recognized this file.");
        var actors = (parsed?.Actors ?? profile.ReadActors(worldSave.Span)).ToDictionary(a => a.ActorGuid);
        var spawns = parsed?.Spawns ?? profile.ReadMolarSpawns(worldSave.Span);
        var selected = spawns.Select(spawn =>
        {
            PersistentActorRecord? actor = null;
            if (spawn.ActorGuid is { } id) actors.TryGetValue(id, out actor);
            var resolution = stateResolver.Resolve(spawn, actor);
            return spawn with { State = resolution.State, ApproachState = resolution.ApproachState };
        }).ToArray();
        var collected = selected.Where(x => x.State == MolarState.Collected).ToArray();
        var uncollected = selected.Where(x => x.State == MolarState.Uncollected).ToArray();
        var unknown = selected.Where(x => x.State == MolarState.Unknown).ToArray();
        return new(selected, collected, uncollected, unknown, unknown.Length == 0 ? SaveConfidence.Validated : SaveConfidence.Partial, $"Parsed with {profile.Name}; selected={selected.Length}.");
    }
}
public sealed class SaveAnalysisService(ISaveDecoder decoder, IMolarAnalyzer analyzer)
{
    public MolarAnalysis Analyze(string path) => analyzer.Analyze(decoder.Decode(path));
}
public sealed class ConservativeStateResolver : IMolarStateResolver { public MolarResolution Resolve(MolarSpawn spawn, PersistentActorRecord? actor) => new(MolarState.Unknown, MolarApproachState.Unknown); }
