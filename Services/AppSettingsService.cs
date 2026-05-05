using System.IO;
using System.Text.Json;

namespace PlaneLoadoutWpfTest.Services;

/// <summary>
/// Loads and saves <see cref="AppSettings"/> to
/// %AppData%\FunkUI\settings.json.
/// </summary>
public static class AppSettingsService
{
    private static readonly string _folder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FunkUI");

    private static readonly string _path = Path.Combine(_folder, "settings.json");

    private static readonly JsonSerializerOptions _jsonOpts =
        new() { WriteIndented = true };

    private static AppSettings _current = Load();
    public static AppSettings Current => _current;
    public static event Action<AppSettings>? SettingsSaved;

    // ── Load ──────────────────────────────────────────────────────────────────

    private static AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                return Normalize(JsonSerializer.Deserialize<AppSettings>(json) ?? Defaults());
            }
        }
        catch { }
        return Defaults();
    }

    // ── Save ──────────────────────────────────────────────────────────────────

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(_folder);
            File.WriteAllText(_path, JsonSerializer.Serialize(settings, _jsonOpts));
            _current = settings;
            SettingsSaved?.Invoke(_current);
        }
        catch { }
    }

    // ── Defaults ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Factory defaults pre-populated from the known Steam install and
    /// Documents locations on this machine.
    /// </summary>
    private static AppSettings Defaults()
    {
        const string steam =
            @"G:\SteamLibrary\steamapps\common\IL-2 Sturmovik Cliffs of Dover Blitz";

        return new AppSettings
        {
            GameRootPath = steam,
            DocumentsRootPath =
                @"C:\Users\meise\OneDrive\Documents",
            BoBSingleMissionsPath    = Path.Combine(steam, @"parts\bob\missions\Single\"),
            TobrukSingleMissionsPath = Path.Combine(steam, @"parts\tobruk\missions\Single\"),
            QuickMissionsPath        = Path.Combine(steam, @"parts\tobruk\mission\Quick\"),
            LennyCampaignsPath       = string.Empty,   // user fills in manually
            UserMissionsPath         =
                @"C:\Users\meise\OneDrive\Documents\1C SoftClub\il-2 sturmovik cliffs of dover\mission\quick\"
        };
    }

    private static AppSettings Normalize(AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.GameRootPath))
            settings.GameRootPath = MissionPathResolver.InferGameRoot(settings);

        if (string.IsNullOrWhiteSpace(settings.DocumentsRootPath))
            settings.DocumentsRootPath = MissionPathResolver.InferDocumentsRoot(settings);

        return settings;
    }
}
