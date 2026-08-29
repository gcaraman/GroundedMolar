using System.ComponentModel;

namespace GroundedMolar.Core;

public sealed class PassThroughSaveDecoder(int maximumBytes = GroundedCsavDecoder.DefaultMaximumDecodedSize) : ISaveDecoder
{
    public byte[] Decode(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.SequentialScan);
        var length = stream.Length;
        if (length is <= 0 || length > maximumBytes)
            throw new InvalidDataException($"Decoded save size {length:N0} is outside the allowed range.");
        var bytes = GC.AllocateUninitializedArray<byte>(checked((int)length));
        stream.ReadExactly(bytes);
        if (stream.Length != length) throw new IOException("The decoded save changed while it was being read.");
        return bytes;
    }
}
public sealed class ProfiledMolarAnalyzer(IEnumerable<ISaveFormatProfile> profiles, IMolarStateResolver stateResolver) : IMolarAnalyzer
{
    private readonly ISaveFormatProfile[] _profiles = profiles.ToArray();
    public MolarAnalysis Analyze(ReadOnlyMemory<byte> worldSave) => Analyze(worldSave, CancellationToken.None);

    public MolarAnalysis Analyze(ReadOnlyMemory<byte> worldSave, CancellationToken cancellationToken)
    {
        ISaveFormatProfile? profile = null;
        ParsedSaveFormat? parsed = null;
        var rejectionReasons = new List<string>();
        foreach (var candidate in _profiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (candidate is IAtomicSaveFormatProfile atomic)
            {
                try
                {
                    parsed = candidate is GroundedSaveFormatProfileV1 grounded
                        ? grounded.Parse(worldSave.Span, cancellationToken)
                        : atomic.Parse(worldSave.Span);
                    profile = candidate; break;
                }
                catch (InvalidDataException exception) { rejectionReasons.Add($"{candidate.Name}: {exception.Message}"); }
                catch (ArgumentOutOfRangeException exception) { rejectionReasons.Add($"{candidate.Name}: {exception.Message}"); }
            }
            else if (candidate.CanParse(worldSave.Span)) { profile = candidate; break; }
        }
        if (profile is null)
        {
            var diagnostic = rejectionReasons.Count == 0
                ? "No validated save-format profile recognized this file."
                : $"No validated save-format profile recognized this file. {string.Join(" ", rejectionReasons)}";
            return MolarAnalysis.Unsupported(diagnostic);
        }
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
    public MolarAnalysis Analyze(string path) => Analyze(path, CancellationToken.None);

    public MolarAnalysis Analyze(string path, CancellationToken cancellationToken)
    {
        try
        {
            var decoded = decoder is GroundedCsavDecoder csav ? csav.Decode(path, cancellationToken) : decoder.Decode(path);
            cancellationToken.ThrowIfCancellationRequested();
            return analyzer is ProfiledMolarAnalyzer profiled ? profiled.Analyze(decoded, cancellationToken) : analyzer.Analyze(decoded);
        }
        catch (Exception exception) when (exception is InvalidDataException or TimeoutException or Win32Exception or PlatformNotSupportedException)
        {
            return MolarAnalysis.Unsupported($"Save decoding failed closed: {exception.Message}");
        }
    }
}
public sealed class ConservativeStateResolver : IMolarStateResolver { public MolarResolution Resolve(MolarSpawn spawn, PersistentActorRecord? actor) => new(MolarState.Unknown, MolarApproachState.Unknown); }
