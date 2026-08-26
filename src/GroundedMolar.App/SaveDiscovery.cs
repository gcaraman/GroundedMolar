using System.IO;

namespace GroundedMolar.App;

internal static class SaveDiscovery
{
    public static string? FindCurrentSave(string folder, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return null;

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            MatchCasing = MatchCasing.CaseInsensitive
        };
        string? newestPath = null;
        var newestWriteTime = DateTime.MinValue;
        foreach (var path in Directory.EnumerateFiles(folder, "World.csav", options))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var file = new FileInfo(path);
            var writeTime = file.LastWriteTimeUtc;
            if (newestPath is null || writeTime > newestWriteTime ||
                writeTime == newestWriteTime && StringComparer.OrdinalIgnoreCase.Compare(file.FullName, newestPath) < 0)
            {
                newestPath = file.FullName;
                newestWriteTime = writeTime;
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        return newestPath;
    }
}
