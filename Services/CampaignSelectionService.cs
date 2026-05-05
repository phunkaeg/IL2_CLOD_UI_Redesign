using System.IO;
using System.Text;

namespace PlaneLoadoutWpfTest.Services;

public sealed class CampaignSelectionItem
{
    public string Title { get; init; } = "";
    public string Source { get; init; } = "";
    public string Root { get; init; } = "";
    public string Period { get; init; } = "";
    public string Summary { get; init; } = "";
    public string Progress { get; init; } = "";
    public CampaignBoardCampaign Campaign { get; init; } = new();
    public string Theater => Campaign.Map.Theater;
    public string Description => string.IsNullOrWhiteSpace(Campaign.Description)
        ? "No campaign description detected."
        : Campaign.Description;
    public string PosterImage => Campaign.PosterImage;
    public string SourceLine => string.Join(" / ", new[] { Source, Progress, Period }.Where(s => !string.IsNullOrWhiteSpace(s)));
    public string DetailLine => $"{Summary} / {Root}";
}

public static class CampaignSelectionService
{
    private static IReadOnlyList<CampaignSelectionItem>? _cachedCampaigns;

    public static bool HasCache => _cachedCampaigns is not null;

    public static void ClearCache()
        => _cachedCampaigns = null;

    public static IReadOnlyList<CampaignSelectionItem> DiscoverCampaigns(bool forceRefresh = false)
    {
        if (!forceRefresh && _cachedCampaigns is not null) return _cachedCampaigns;

        var settings = AppSettingsService.Current;
        var candidates = new List<(string Source, string Root)>();

        foreach (var campaignRoot in MissionPathResolver.ExistingPaths(MissionPathResolver.CampaignRoots(settings)))
            candidates.Add(("Cliffs of Dover", campaignRoot));

        if (!string.IsNullOrWhiteSpace(settings.LennyCampaignsPath) && Directory.Exists(settings.LennyCampaignsPath))
            candidates.Add(("Lenny Campaigns", settings.LennyCampaignsPath));

        candidates.AddRange(FindNestedCampaignRoots(settings).Select(root => ("User Campaigns", root)));

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<CampaignSelectionItem>();

        foreach (var (source, root) in candidates.Where(c => Directory.Exists(c.Root)))
        {
            foreach (var campaign in DiscoverFromRoot(source, root))
            {
                var key = campaign.SourceRoot;
                if (string.IsNullOrWhiteSpace(key)) key = $"{source}|{campaign.Title}";
                if (!seen.Add(key)) continue;

                result.Add(new CampaignSelectionItem
                {
                    Title = campaign.Title,
                    Source = source,
                    Root = campaign.SourceRoot,
                    Period = campaign.Period,
                    Summary = campaign.Summary,
                    Progress = source.Equals("Cliffs of Dover", StringComparison.OrdinalIgnoreCase) ? FindVanillaProgress(campaign.SourceRoot) : "",
                    Campaign = campaign
                });
            }
        }

        var ordered = result
            .OrderBy(i => i.Source, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _cachedCampaigns = ordered;
        CampaignBoardSession.Remember(ordered.Select(i => i.Campaign));
        return ordered;
    }

    public static string BuildDiscoveryDiagnostics()
    {
        var settings = AppSettingsService.Current;
        var sb = new StringBuilder();
        sb.AppendLine("# Campaign Discovery Diagnostics");
        sb.AppendLine();
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("## Configured Roots");
        sb.AppendLine();
        sb.AppendLine($"- Game root: `{settings.GameRootPath}`");
        sb.AppendLine($"- Documents root: `{settings.DocumentsRootPath}`");
        sb.AppendLine($"- Legacy Lenny path: `{settings.LennyCampaignsPath}`");
        sb.AppendLine();

        sb.AppendLine("## Vanilla Campaign Roots");
        sb.AppendLine();
        foreach (var root in MissionPathResolver.CampaignRoots(settings).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine($"- `{root}`");
            sb.AppendLine($"  - Exists: {Directory.Exists(root)}");
            if (Directory.Exists(root))
                sb.AppendLine($"  - campaigns.ini: {File.Exists(Path.Combine(root, "campaigns.ini"))}");
        }

        sb.AppendLine();
        sb.AppendLine("## Nested Campaign Search Roots");
        sb.AppendLine();
        var nestedRoots = MissionPathResolver.SingleMissionRoots(settings)
            .Select(e => e.Path)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var root in nestedRoots)
        {
            sb.AppendLine($"- `{root}`");
            sb.AppendLine($"  - Exists: {Directory.Exists(root)}");
            if (!Directory.Exists(root)) continue;

            var candidates = DiscoverCampaignSubroots(root)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
            sb.AppendLine($"  - Accepted nested campaign candidates: {candidates.Count}");
            foreach (var candidate in candidates.Take(80))
                sb.AppendLine($"    - `{candidate}`");
            if (candidates.Count > 80)
                sb.AppendLine($"    - ... {candidates.Count - 80} more");
        }

        sb.AppendLine();
        sb.AppendLine("## Current Campaign Selector Result");
        sb.AppendLine();
        var campaigns = DiscoverCampaigns(true);
        sb.AppendLine($"Detected campaigns: {campaigns.Count}");
        foreach (var campaign in campaigns)
            sb.AppendLine($"- {campaign.Title} / {campaign.Theater} / `{campaign.Root}`");

        sb.AppendLine();
        sb.AppendLine("## Expected Lenny Placement");
        sb.AppendLine();
        sb.AppendLine("For automatic discovery without a custom Lenny path, place campaign master folders under:");
        foreach (var root in MissionPathResolver.SingleMissionRoots(settings)
                     .Where(e => e.Label.Contains("Documents", StringComparison.OrdinalIgnoreCase))
                     .Select(e => e.Path)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
            sb.AppendLine($"- `{root}`");

        return sb.ToString();
    }

    private static IReadOnlyList<CampaignBoardCampaign> DiscoverFromRoot(string source, string root)
    {
        if (source.Equals("Cliffs of Dover", StringComparison.OrdinalIgnoreCase))
        {
            var vanilla = DiscoverVanillaCampaigns(root);
            if (vanilla.Count > 0)
            {
                var campaigns = new List<CampaignBoardCampaign>(vanilla);
                var vanillaRoots = vanilla
                    .Select(c => c.SourceRoot)
                    .Where(r => !string.IsNullOrWhiteSpace(r))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var extraRoot in DiscoverCampaignSubroots(root)
                             .Where(r => !r.Equals(root, StringComparison.OrdinalIgnoreCase))
                             .Where(r => !vanillaRoots.Contains(r))
                             .Where(r => !Path.GetFileName(r).StartsWith("campaign_", StringComparison.OrdinalIgnoreCase)))
                {
                    campaigns.AddRange(CampaignBoardService.ScanCampaignRoot(extraRoot));
                }

                return campaigns
                    .Where(c => c.MissionCount > 0)
                    .GroupBy(c => c.SourceRoot, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();
            }
        }

        return CampaignBoardService.ScanCampaignRoot(root);
    }

    private static IEnumerable<string> FindNestedCampaignRoots(AppSettings settings)
    {
        return MissionPathResolver.ExistingEntries(MissionPathResolver.SingleMissionRoots(settings))
            .Select(e => e.Path)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .SelectMany(DiscoverCampaignSubroots)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> DiscoverCampaignSubroots(string root)
    {
        if (LooksLikeConcreteCampaignRoot(root))
            yield return root;

        foreach (var candidate in DiscoverCampaignSubrootsRecursive(root, 8))
            yield return candidate;
    }

    private static IEnumerable<string> DiscoverCampaignSubrootsRecursive(string root, int depth)
    {
        if (depth <= 0 || !Directory.Exists(root)) yield break;

        foreach (var child in Directory.EnumerateDirectories(root).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            if (Path.GetFileName(child).Equals("Multi", StringComparison.OrdinalIgnoreCase)) continue;

            if (LooksLikeConcreteCampaignRoot(child))
            {
                yield return child;
                continue;
            }

            foreach (var descendant in DiscoverCampaignSubrootsRecursive(child, depth - 1))
                yield return descendant;
        }
    }

    private static bool LooksLikeConcreteCampaignRoot(string folder)
    {
        if (!Directory.Exists(folder)) return false;
        var childDirs = Directory.EnumerateDirectories(folder).ToList();
        if (File.Exists(Path.Combine(folder, "campaigns.ini"))) return true;
        if (childDirs.Any(d => System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(d), @"^\d{4}-\d{2}-\d{2}\s*-"))) return true;
        if (childDirs.Any(d => System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(d), @"^\d{3,4}\s*-"))) return true;
        if (childDirs.Any(d => System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(d), @"^\d+\s*-\s*(high|medium|low|vr|full)", System.Text.RegularExpressions.RegexOptions.IgnoreCase))) return true;
        return false;
    }

    private static List<CampaignBoardCampaign> DiscoverVanillaCampaigns(string root)
    {
        var ini = Path.Combine(root, "campaigns.ini");
        if (!File.Exists(ini)) return [];

        var entries = ParseVanillaCampaignsIni(ini);
        var campaigns = new List<CampaignBoardCampaign>();
        foreach (var entry in entries)
        {
            var folder = Path.Combine(root, entry.Folder);
            if (!Directory.Exists(folder)) continue;

            var parsed = CampaignBoardService.ScanCampaignRoot(folder).FirstOrDefault();
            if (parsed is null || parsed.MissionCount == 0) continue;
            campaigns.Add(CloneWithTitle(parsed, entry.Title, FindVanillaBattleIndex(folder) ?? 0));
        }

        return campaigns;
    }

    private static CampaignBoardCampaign CloneWithTitle(CampaignBoardCampaign campaign, string title, int? unlockedMissionIndex = null)
        => new()
        {
            Title = string.IsNullOrWhiteSpace(title) ? campaign.Title : title,
            Period = campaign.Period,
            SourceRoot = campaign.SourceRoot,
            Description = campaign.Description,
            PosterImage = campaign.PosterImage,
            Map = campaign.Map,
            Days = unlockedMissionIndex is null ? campaign.Days : CloneDaysWithLocks(campaign.Days, unlockedMissionIndex.Value)
        };

    private static List<CampaignDay> CloneDaysWithLocks(List<CampaignDay> days, int unlockedMissionIndex)
    {
        var missionIndex = 0;
        var result = new List<CampaignDay>();
        foreach (var day in days)
        {
            var missions = new List<CampaignMission>();
            foreach (var mission in day.Missions)
            {
                missions.Add(CloneMissionWithLock(mission, missionIndex > unlockedMissionIndex));
                missionIndex++;
            }

            result.Add(new CampaignDay
            {
                Date = day.Date,
                Title = day.Title,
                Summary = day.Summary,
                Weather = day.Weather,
                Missions = missions
            });
        }

        return result;
    }

    private static CampaignMission CloneMissionWithLock(CampaignMission mission, bool isLocked)
        => new()
        {
            Date = mission.Date,
            Time = mission.Time,
            Title = mission.Title,
            Type = mission.Type,
            Briefing = mission.Briefing,
            Weather = mission.Weather,
            Map = mission.Map,
            Playable = mission.Playable,
            Routes = mission.Routes,
            Slides = mission.Slides,
            Images = mission.Images,
            IntelLocations = mission.IntelLocations,
            SourceFiles = mission.SourceFiles,
            ChapterTitle = mission.ChapterTitle,
            IsLocked = isLocked
        };

    private static List<(string Folder, string Title)> ParseVanillaCampaignsIni(string path)
    {
        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();
        var current = "";

        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(';')) continue;
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                current = line[1..^1].Trim();
                sections.TryAdd(current, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
                continue;
            }

            if (current.Equals("Main", StringComparison.OrdinalIgnoreCase))
            {
                order.Add(line);
                continue;
            }

            if (string.IsNullOrWhiteSpace(current)) continue;
            var split = line.IndexOf(' ');
            if (split <= 0) continue;
            sections[current][line[..split].Trim()] = line[(split + 1)..].Trim();
        }

        return order
            .Select(key =>
            {
                sections.TryGetValue(key, out var section);
                var folder = section?.GetValueOrDefault("folder") ?? key;
                var title = section?.GetValueOrDefault("title") ?? folder;
                return (Folder: folder, Title: title);
            })
            .ToList();
    }

    private static string FindVanillaProgress(string campaignRoot)
    {
        var folder = Path.GetFileName(campaignRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(folder)) return "";

        foreach (var root in PossibleStateRoots())
        {
            var path = Path.Combine(root, $"bob.{folder}.state.ini");
            if (!File.Exists(path)) continue;

            var index = ReadBattleIndex(path);
            if (index is not null) return $"Progress mission {index.Value + 1}";
        }

        return "New campaign";
    }

    private static int? FindVanillaBattleIndex(string campaignRoot)
    {
        var folder = Path.GetFileName(campaignRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(folder)) return null;

        foreach (var root in PossibleStateRoots())
        {
            var path = Path.Combine(root, $"bob.{folder}.state.ini");
            if (File.Exists(path)) return ReadBattleIndex(path);
        }

        return null;
    }

    private static int? ReadBattleIndex(string path)
    {
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            var parts = line.Split([' ', '='], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !parts[0].Equals("battleIndex", StringComparison.OrdinalIgnoreCase)) continue;
            return int.TryParse(parts[1], out var index) ? index : null;
        }

        return null;
    }

    private static IEnumerable<string> PossibleStateRoots()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrWhiteSpace(documents))
            yield return Path.Combine(documents, @"1C SoftClub\il-2 sturmovik cliffs of dover\mission\campaign");

        var oneDrive = Environment.GetEnvironmentVariable("OneDrive");
        if (!string.IsNullOrWhiteSpace(oneDrive))
            yield return Path.Combine(oneDrive, @"Documents\1C SoftClub\il-2 sturmovik cliffs of dover\mission\campaign");

        yield return @"C:\Users\meise\OneDrive\Documents\1C SoftClub\il-2 sturmovik cliffs of dover\mission\campaign";
    }

}
