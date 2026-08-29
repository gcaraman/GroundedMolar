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
    private static readonly string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MolarMap");
    private static readonly string FilePath = Path.Combine(DirectoryPath, "settings.json");
    private static readonly string LegacyFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GroundedMolar", "settings.json");
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static bool HasChanged(AppSettings persisted, AppSettings current) => persisted != current;

    public static AppSettings Load() => Load(FilePath, LegacyFilePath);

    internal static AppSettings Load(string filePath, string legacyFilePath)
    {
        try
        {
            if (File.Exists(filePath)) return Deserialize(filePath);
            if (!File.Exists(legacyFilePath)) return AppSettings.Default;

            var settings = Deserialize(legacyFilePath);
            try { Save(settings, filePath); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
            return settings;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException) { return AppSettings.Default; }
    }

    public static void Save(AppSettings settings) => Save(settings, FilePath);

    internal static void Save(AppSettings settings, string filePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var temporary = filePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings, Options));
        File.Move(temporary, filePath, true);
    }

    private static AppSettings Deserialize(string filePath) =>
        JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(filePath), Options) ?? AppSettings.Default;
}
