using System.IO;

namespace PlaneLoadoutWpfTest.Services;

public sealed record MissionPathEntry(string Path, string Label, MissionCategory Category);

public static class MissionPathResolver
{
    private const string SoftClubFolder = @"1C SoftClub\il-2 sturmovik cliffs of dover";

    public static IEnumerable<MissionPathEntry> SingleMissionRoots(AppSettings settings)
    {
        foreach (var root in GameRoots(settings))
        {
            yield return new MissionPathEntry(Path.Combine(root, @"missions\Single"), "Game Single Missions", MissionCategory.BoBSingle);
            yield return new MissionPathEntry(Path.Combine(root, @"parts\bob\missions\Single"), "BoB Single Missions", MissionCategory.BoBSingle);
            yield return new MissionPathEntry(Path.Combine(root, @"parts\tobruk\missions\Single"), "Tobruk Single Missions", MissionCategory.TobrukSingle);
        }

        foreach (var root in DocumentsCliffsRoots(settings))
            yield return new MissionPathEntry(Path.Combine(root, @"missions\single"), "Documents Single Missions", MissionCategory.UserMission);

        if (!string.IsNullOrWhiteSpace(settings.BoBSingleMissionsPath))
            yield return new MissionPathEntry(settings.BoBSingleMissionsPath, "Legacy BoB Single Missions", MissionCategory.BoBSingle);
        if (!string.IsNullOrWhiteSpace(settings.TobrukSingleMissionsPath))
            yield return new MissionPathEntry(settings.TobrukSingleMissionsPath, "Legacy Tobruk Single Missions", MissionCategory.TobrukSingle);
        if (!string.IsNullOrWhiteSpace(settings.UserMissionsPath))
            yield return new MissionPathEntry(settings.UserMissionsPath, "Legacy User Missions", MissionCategory.UserMission);
        if (!string.IsNullOrWhiteSpace(settings.LennyCampaignsPath))
            yield return new MissionPathEntry(settings.LennyCampaignsPath, "Legacy Lenny Campaigns", MissionCategory.LennyCampaign);
    }

    public static IEnumerable<MissionPathEntry> QuickMissionRoots(AppSettings settings)
    {
        foreach (var root in GameRoots(settings))
        {
            yield return new MissionPathEntry(Path.Combine(root, @"parts\bob\mission\Quick"), "BoB Quick Missions", MissionCategory.Quick);
            yield return new MissionPathEntry(Path.Combine(root, @"parts\tobruk\mission\Quick"), "Tobruk Quick Missions", MissionCategory.Quick);
        }

        foreach (var root in DocumentsCliffsRoots(settings))
            yield return new MissionPathEntry(Path.Combine(root, @"mission\quick"), "Documents Quick Missions", MissionCategory.Quick);

        if (!string.IsNullOrWhiteSpace(settings.QuickMissionsPath))
            yield return new MissionPathEntry(settings.QuickMissionsPath, "Legacy Quick Missions", MissionCategory.Quick);
    }

    public static IEnumerable<string> CampaignRoots(AppSettings settings)
    {
        foreach (var root in GameRoots(settings))
        {
            yield return Path.Combine(root, @"parts\bob\mission\campaign");
            yield return Path.Combine(root, @"parts\tobruk\mission\campaign");
        }

        foreach (var root in DocumentsCliffsRoots(settings))
            yield return Path.Combine(root, @"mission\campaign");
    }

    public static IEnumerable<string> MultiplayerRoots(AppSettings settings)
    {
        foreach (var root in GameRoots(settings))
        {
            yield return Path.Combine(root, @"missions\Multi");
            yield return Path.Combine(root, @"parts\bob\missions\Multi");
            yield return Path.Combine(root, @"parts\tobruk\missions\Multi");
        }

        foreach (var root in DocumentsCliffsRoots(settings))
            yield return Path.Combine(root, @"missions\Multi");
    }

    public static string InferGameRoot(AppSettings settings)
    {
        foreach (var path in new[]
                 {
                     settings.BoBSingleMissionsPath,
                     settings.TobrukSingleMissionsPath,
                     settings.QuickMissionsPath
                 })
        {
            var root = WalkUpToGameRoot(path);
            if (!string.IsNullOrWhiteSpace(root)) return root;
        }

        const string known = @"G:\SteamLibrary\steamapps\common\IL-2 Sturmovik Cliffs of Dover Blitz";
        return Directory.Exists(known) ? known : "";
    }

    public static string InferDocumentsRoot(AppSettings settings)
    {
        foreach (var path in new[] { settings.UserMissionsPath, settings.LennyCampaignsPath })
        {
            var root = WalkUpToDocumentsRoot(path);
            if (!string.IsNullOrWhiteSpace(root)) return root;
        }

        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrWhiteSpace(documents)) return documents;

        var oneDrive = Environment.GetEnvironmentVariable("OneDrive");
        return string.IsNullOrWhiteSpace(oneDrive) ? "" : Path.Combine(oneDrive, "Documents");
    }

    public static IReadOnlyList<string> ExistingPaths(IEnumerable<string> paths)
        => paths.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    public static IReadOnlyList<MissionPathEntry> ExistingEntries(IEnumerable<MissionPathEntry> entries)
        => entries.Where(e => Directory.Exists(e.Path))
            .GroupBy(e => e.Path, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

    private static IEnumerable<string> GameRoots(AppSettings settings)
    {
        var roots = new[]
        {
            settings.GameRootPath,
            InferGameRoot(settings)
        };

        foreach (var root in roots.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.OrdinalIgnoreCase))
            yield return root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static IEnumerable<string> DocumentsCliffsRoots(AppSettings settings)
    {
        foreach (var root in DocumentsRoots(settings))
        {
            if (Path.GetFileName(root).Equals("il-2 sturmovik cliffs of dover", StringComparison.OrdinalIgnoreCase))
            {
                yield return root;
                continue;
            }

            if (Path.GetFileName(root).Equals("1C SoftClub", StringComparison.OrdinalIgnoreCase))
            {
                yield return Path.Combine(root, "il-2 sturmovik cliffs of dover");
                continue;
            }

            yield return Path.Combine(root, SoftClubFolder);
        }
    }

    private static IEnumerable<string> DocumentsRoots(AppSettings settings)
    {
        var roots = new[]
        {
            settings.DocumentsRootPath,
            InferDocumentsRoot(settings)
        };

        foreach (var root in roots.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.OrdinalIgnoreCase))
            yield return root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static string WalkUpToGameRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "";
        var dir = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path).Directory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Launcher64.exe"))
                || Directory.Exists(Path.Combine(dir.FullName, "parts", "bob"))
                || Directory.Exists(Path.Combine(dir.FullName, "parts", "tobruk")))
                return dir.FullName;

            dir = dir.Parent;
        }

        return "";
    }

    private static string WalkUpToDocumentsRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "";
        var dir = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path).Directory;
        while (dir is not null)
        {
            if (dir.Name.Equals("Documents", StringComparison.OrdinalIgnoreCase))
                return dir.FullName;

            if (dir.Parent is not null
                && dir.Parent.Name.Equals("1C SoftClub", StringComparison.OrdinalIgnoreCase))
                return dir.Parent.Parent?.FullName ?? "";

            dir = dir.Parent;
        }

        return "";
    }
}
