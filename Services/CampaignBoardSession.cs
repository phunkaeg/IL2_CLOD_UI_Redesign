using System.IO;
using Microsoft.Win32;

namespace PlaneLoadoutWpfTest.Services;

public static class CampaignBoardSession
{
    private static IReadOnlyList<CampaignBoardCampaign> _campaigns = [];

    public static IReadOnlyList<CampaignBoardCampaign> Campaigns => _campaigns;

    public static bool HasCampaigns => _campaigns.Count > 0;

    public static void Remember(IEnumerable<CampaignBoardCampaign> campaigns)
        => _campaigns = campaigns.ToList();

    public static string ExportJson()
    {
        var path = PickExportPath("campaign-board-export.json", "JSON files (*.json)|*.json");
        if (string.IsNullOrWhiteSpace(path)) return "";

        File.WriteAllText(path, CampaignBoardExportService.ToJson(_campaigns));
        return path;
    }

    public static string ExportDiagnostics()
    {
        var path = PickExportPath("campaign-parser-diagnostics.md", "Markdown files (*.md)|*.md");
        if (string.IsNullOrWhiteSpace(path)) return "";

        File.WriteAllText(path, CampaignBoardExportService.BuildDiagnostics(_campaigns));
        return path;
    }

    private static string PickExportPath(string fileName, string filter)
    {
        var dialog = new SaveFileDialog
        {
            FileName = fileName,
            Filter = filter,
            AddExtension = true,
            OverwritePrompt = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : "";
    }
}
