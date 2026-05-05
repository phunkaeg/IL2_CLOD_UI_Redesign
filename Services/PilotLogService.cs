using System.IO;
using System.Text.Json;

namespace PlaneLoadoutWpfTest.Services;

public sealed class PilotLogEntry
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public DateTime SavedAtUtc { get; init; } = DateTime.UtcNow;
    public string Campaign { get; init; } = "";
    public string Date { get; init; } = "";
    public string MissionTime { get; init; } = "";
    public string MissionTitle { get; init; } = "";
    public string Unit { get; init; } = "";
    public string Side { get; init; } = "";
    public string Aircraft { get; init; } = "";
    public string Result { get; init; } = "";
    public string Landing { get; init; } = "";
    public int AirClaims { get; init; }
    public int GroundClaims { get; init; }
    public string Damage { get; init; } = "";
    public string Notes { get; init; } = "";
    public string Display => $"{Date} {MissionTime} - {MissionTitle}";
    public string Summary => $"{Unit} / {Aircraft} / {Result}";
}

public static class PilotLogService
{
    private static readonly JsonSerializerOptions s_options = new() { WriteIndented = true };

    public static IReadOnlyList<PilotLogEntry> Load()
    {
        var path = LogPath();
        if (!File.Exists(path)) return [];

        try
        {
            return JsonSerializer.Deserialize<List<PilotLogEntry>>(File.ReadAllText(path), s_options) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static void Save(PilotLogEntry entry)
    {
        var entries = Load().ToList();
        entries.Insert(0, entry);
        Directory.CreateDirectory(Path.GetDirectoryName(LogPath())!);
        File.WriteAllText(LogPath(), JsonSerializer.Serialize(entries, s_options));
    }

    public static string LogPath()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PlaneLoadoutWpfTest",
            "pilot-log.json");
}
