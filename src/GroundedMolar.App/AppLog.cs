using System.IO;
using System.Text;

namespace GroundedMolar.App;

internal static class AppLog
{
    private static readonly object Gate = new();
    public static string DirectoryPath => Path.Combine(AppContext.BaseDirectory, "logs");

    public static void Error(string context, Exception exception) => Error(context, exception.ToString());

    public static void Error(string context, string? detail)
    {
        lock (Gate)
        {
            var entry = $"{DateTimeOffset.UtcNow:O} ERROR {context}{Environment.NewLine}{detail}{Environment.NewLine}";
            if (!TryWrite(DirectoryPath, entry))
                TryWrite(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MolarMap", "logs"), entry);
        }
    }

    internal static bool TryWrite(string directory, string entry)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "MolarMap.log");
            if (File.Exists(path) && new FileInfo(path).Length >= 2 * 1024 * 1024)
                File.Move(path, path + ".previous", true);
            File.AppendAllText(path, entry, Encoding.UTF8);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException or NotSupportedException or ArgumentException)
        {
            return false;
        }
    }
}
