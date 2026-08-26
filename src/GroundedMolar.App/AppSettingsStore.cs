using System.IO;
using System.Text.Json;
using GroundedMolar.Core;
namespace GroundedMolar.App;

internal sealed record AppSettings(
    string SavePath,
    string SaveFolder,
    bool MonitorFolder,
    double UnapproachedOpacity = MolarMarkerOpacity.DefaultUnapproached,
    bool ShowGuideHints = false)
{
    public static AppSettings Default { get; } = new("", "", false, MolarMarkerOpacity.DefaultUnapproached, false);
}

internal static class AppSettingsStore
{
    private static readonly string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GroundedMolar");
    private static readonly string FilePath = Path.Combine(DirectoryPath, "settings.json");
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static bool HasChanged(AppSettings persisted, AppSettings current) => persisted != current;

    public static AppSettings Load()
    {
        try { return File.Exists(FilePath) ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Options) ?? AppSettings.Default : AppSettings.Default; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException) { return AppSettings.Default; }
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(DirectoryPath);
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings, Options));
        File.Move(temporary, FilePath, true);
    }
}
