namespace PlaneLoadoutWpfTest.Services;

/// <summary>
/// Shared, in-memory mission catalog populated by the settings overlay's
/// "Scan and Populate" action.  Screens subscribe to <see cref="CatalogUpdated"/>
/// to refresh their lists when a new scan completes.
/// </summary>
public static class MissionCatalog
{
    // ── Backing store ─────────────────────────────────────────────────────────

    private static readonly List<ScannedMission> _all = new();
    private static bool _hasPopulated;

    // ── Public read-only views ────────────────────────────────────────────────

    public static IReadOnlyList<ScannedMission> All =>
        _all;

    public static bool HasPopulated => _hasPopulated;

    public static IReadOnlyList<ScannedMission> BoBSingleMissions =>
        _all.Where(m => m.Category == MissionCategory.BoBSingle
                        || (m.Category == MissionCategory.UserMission && !IsTobrukMap(m))).ToList();

    public static IReadOnlyList<ScannedMission> TobrukSingleMissions =>
        _all.Where(m => m.Category == MissionCategory.TobrukSingle
                        || (m.Category == MissionCategory.UserMission && IsTobrukMap(m))).ToList();

    /// <summary>All single missions (BoB + Tobruk combined).</summary>
    public static IReadOnlyList<ScannedMission> AllSingleMissions =>
        _all.Where(m => m.Category is MissionCategory.BoBSingle
                                   or MissionCategory.TobrukSingle
                                   or MissionCategory.UserMission).ToList();

    public static IReadOnlyList<ScannedMission> QuickMissions =>
        _all.Where(m => m.Category == MissionCategory.Quick).ToList();

    public static IReadOnlyList<ScannedMission> LennyCampaigns =>
        _all.Where(m => m.Category == MissionCategory.LennyCampaign).ToList();

    public static IReadOnlyList<ScannedMission> UserMissions =>
        _all.Where(m => m.Category == MissionCategory.UserMission).ToList();

    private static bool IsTobrukMap(ScannedMission mission)
        => mission.Map.Contains("tobruk", StringComparison.OrdinalIgnoreCase)
           || mission.Map.Contains("desert", StringComparison.OrdinalIgnoreCase)
           || mission.Map.Contains("libya", StringComparison.OrdinalIgnoreCase);

    // ── Change notification ───────────────────────────────────────────────────

    /// <summary>
    /// Raised on the UI thread after <see cref="PopulateAsync"/> completes.
    /// </summary>
    public static event Action? CatalogUpdated;

    // ── Population ────────────────────────────────────────────────────────────

    /// <summary>
    /// Scans all configured paths off the UI thread, then replaces the catalog
    /// contents and raises <see cref="CatalogUpdated"/> on the dispatcher.
    /// </summary>
    public static async Task PopulateAsync(
        AppSettings                        settings,
        System.Windows.Threading.Dispatcher dispatcher,
        IProgress<string>?                 progress = null)
    {
        var results = await Task.Run(() =>
        {
            var list = new List<ScannedMission>();

            void Scan(string path, MissionCategory cat, string label)
            {
                if (string.IsNullOrWhiteSpace(path)) return;
                progress?.Report($"Scanning {label}…");
                list.AddRange(MissionScannerService.ScanFolder(path, cat));
            }

            foreach (var entry in MissionPathResolver.ExistingEntries(MissionPathResolver.SingleMissionRoots(settings)))
                Scan(entry.Path, entry.Category, entry.Label);

            foreach (var entry in MissionPathResolver.ExistingEntries(MissionPathResolver.QuickMissionRoots(settings)))
                Scan(entry.Path, entry.Category, entry.Label);

            return list;
        });

        await dispatcher.InvokeAsync(() =>
        {
            _all.Clear();
            _all.AddRange(results);
            _hasPopulated = true;
            CatalogUpdated?.Invoke();
        });
    }
}
