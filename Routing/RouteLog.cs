using System.Text.Json;

namespace BrowserWitch.Routing;

public class RouteLogEntry
{
    public DateTime Timestamp { get; set; }
    public string OriginalUrl { get; set; } = "";
    public string ResolvedUrl { get; set; } = "";
    public string CleanedUrl { get; set; } = "";
    public string BrowserKey { get; set; } = "";
}

/// <summary>
/// File-backed route log so entries from short-lived URL-routing processes
/// are visible to the long-running tray app.
/// </summary>
public static class RouteLog
{
    private const int MaxEntries = 50;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private static string LogPath => Path.Combine(AppContext.BaseDirectory, "routelog.json");

    public static void Add(RouteLogEntry entry)
    {
        try
        {
            var entries = LoadEntries();
            entries.Add(entry);
            if (entries.Count > MaxEntries)
                entries.RemoveRange(0, entries.Count - MaxEntries);

            var json = JsonSerializer.Serialize(entries, JsonOptions);
            File.WriteAllText(LogPath, json);
        }
        catch
        {
            // Don't let logging failures break URL routing
        }
    }

    public static List<RouteLogEntry> GetEntries()
    {
        return LoadEntries();
    }

    public static void Clear()
    {
        try { File.Delete(LogPath); } catch { }
    }

    private static List<RouteLogEntry> LoadEntries()
    {
        try
        {
            if (!File.Exists(LogPath))
                return new List<RouteLogEntry>();

            var json = File.ReadAllText(LogPath);
            return JsonSerializer.Deserialize<List<RouteLogEntry>>(json, JsonOptions)
                   ?? new List<RouteLogEntry>();
        }
        catch
        {
            return new List<RouteLogEntry>();
        }
    }
}
