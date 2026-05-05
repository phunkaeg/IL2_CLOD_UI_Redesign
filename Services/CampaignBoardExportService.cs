using System.Text;
using System.Text.Json;

namespace PlaneLoadoutWpfTest.Services;

public static class CampaignBoardExportService
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true
    };

    public static string ToJson(IEnumerable<CampaignBoardCampaign> campaigns)
        => JsonSerializer.Serialize(campaigns, s_jsonOptions);

    public static string BuildDiagnostics(IEnumerable<CampaignBoardCampaign> campaigns)
    {
        var list = campaigns.ToList();
        var sb = new StringBuilder();

        sb.AppendLine("# Campaign Parser Diagnostics");
        sb.AppendLine();
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Campaigns detected: {list.Count}");
        sb.AppendLine($"Days detected: {list.Sum(c => c.Days.Count)}");
        sb.AppendLine($"Missions detected: {list.Sum(c => c.MissionCount)}");
        sb.AppendLine();

        foreach (var campaign in list)
        {
            sb.AppendLine($"## {campaign.Title}");
            sb.AppendLine();
            sb.AppendLine($"- Source: `{campaign.SourceRoot}`");
            sb.AppendLine($"- Period: {Blank(campaign.Period)}");
            sb.AppendLine($"- Map: `{campaign.Map.Image}` ({campaign.Map.Width} x {campaign.Map.Height})");
            sb.AppendLine($"- Days: {campaign.Days.Count}");
            sb.AppendLine($"- Missions: {campaign.MissionCount}");
            sb.AppendLine();

            AppendSideSummary(sb, campaign);
            AppendMissionTypeSummary(sb, campaign);
            AppendWarnings(sb, campaign);

            sb.AppendLine("### Day / Mission Inventory");
            sb.AppendLine();
            foreach (var day in campaign.Days)
            {
                sb.AppendLine($"- {Blank(day.Date)} / {day.Title}: {day.Missions.Count} missions");
                foreach (var mission in day.Missions)
                {
                    var playable = mission.Playable.Count == 0
                        ? "no playable roles"
                        : string.Join(", ", mission.Playable.Select(r => $"{r.Side}: {r.Unit} / {r.Aircraft}").Take(4));
                    sb.AppendLine($"  - {mission.DisplayTime} | {mission.Title} | {mission.Type} | {playable}");
                }
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static void AppendSideSummary(StringBuilder sb, CampaignBoardCampaign campaign)
    {
        sb.AppendLine("### Side Classification");
        sb.AppendLine();
        foreach (var group in campaign.Days
                     .SelectMany(d => d.Missions)
                     .SelectMany(m => m.Playable.Select(r => r.Side).Concat(m.Routes.Select(r => r.Side)))
                     .GroupBy(s => string.IsNullOrWhiteSpace(s) ? "Unknown" : s)
                     .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine($"- {group.Key}: {group.Count()} flight records");
        }
        sb.AppendLine();
    }

    private static void AppendMissionTypeSummary(StringBuilder sb, CampaignBoardCampaign campaign)
    {
        sb.AppendLine("### Mission Types");
        sb.AppendLine();
        foreach (var group in campaign.Days
                     .SelectMany(d => d.Missions)
                     .GroupBy(m => string.IsNullOrWhiteSpace(m.Type) ? "Unknown" : m.Type)
                     .OrderByDescending(g => g.Count()))
        {
            sb.AppendLine($"- {group.Key}: {group.Count()} missions");
        }
        sb.AppendLine();
    }

    private static void AppendWarnings(StringBuilder sb, CampaignBoardCampaign campaign)
    {
        var warnings = new List<string>();
        foreach (var mission in campaign.Days.SelectMany(d => d.Missions))
        {
            if (mission.Playable.Count == 0)
                warnings.Add($"{mission.DisplayTime} {mission.Title}: no playable roles detected.");
            if (mission.Routes.Count == 0)
                warnings.Add($"{mission.DisplayTime} {mission.Title}: no aircraft routes detected.");
            if (mission.Routes.Any(r => r.Waypoints.Count == 0))
                warnings.Add($"{mission.DisplayTime} {mission.Title}: one or more routes have no waypoints.");
            if (mission.Playable.Any(r => r.Side == "Unknown"))
                warnings.Add($"{mission.DisplayTime} {mission.Title}: one or more playable roles have unknown side.");
            if (mission.Slides.Count == 0 && mission.Images.Count == 0)
                warnings.Add($"{mission.DisplayTime} {mission.Title}: no briefing slides or gallery images found.");
        }

        sb.AppendLine("### Parser Warnings");
        sb.AppendLine();
        if (warnings.Count == 0)
        {
            sb.AppendLine("- No parser warnings detected.");
        }
        else
        {
            foreach (var warning in warnings.Take(200))
                sb.AppendLine($"- {warning}");
            if (warnings.Count > 200)
                sb.AppendLine($"- {warnings.Count - 200} additional warnings omitted from this report.");
        }
        sb.AppendLine();
    }

    private static string Blank(string value)
        => string.IsNullOrWhiteSpace(value) ? "(blank)" : value;
}
