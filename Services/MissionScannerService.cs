using System.IO;
using System.Text.RegularExpressions;

namespace PlaneLoadoutWpfTest.Services;

// ── Mission category ──────────────────────────────────────────────────────────

public enum MissionCategory { BoBSingle, TobrukSingle, Quick, LennyCampaign, UserMission }

// ── Result record ─────────────────────────────────────────────────────────────

public sealed record ScannedWaypoint(string Action, double X, double Y, double Alt, double Speed);

public sealed record ScannedRoute(string Id, string Side, IReadOnlyList<ScannedWaypoint> Waypoints);

/// <param name="FilePath">Absolute path to the .mis file.</param>
/// <param name="Title">Human-readable mission title (HTML stripped).</param>
/// <param name="TimeHHMM">Local mission start time as "HH:MM", e.g. "09:30".</param>
/// <param name="Map">Normalised map name, e.g. "English Channel".</param>
/// <param name="Description">First 200 chars of briefing text (HTML stripped).</param>
/// <param name="BriefingFilePath">Absolute path to the companion .briefing HTML file, or null.</param>
/// <param name="ImagePaths">Absolute paths to images found in or near the mission folder.</param>
/// <param name="Category">Which of the four folder categories this mission belongs to.</param>
/// <param name="CampaignName">Subfolder name used as a campaign grouping label.</param>
public sealed record ScannedMission(
    string                  FilePath,
    string                  Title,
    string                  TimeHHMM,
    string                  Map,
    string                  Description,
    string?                 BriefingFilePath,
    IReadOnlyList<string>   ImagePaths,
    IReadOnlyList<ScannedRoute> Routes,
    MissionCategory         Category,
    string                  CampaignName);

// ── Scanner ───────────────────────────────────────────────────────────────────

/// <summary>
/// Scans an IL-2 Cliffs of Dover missions directory for .mis files and
/// extracts displayable mission metadata.
///
/// CloD .mis files are Windows-INI-style text files.  The [MAIN] section
/// carries the key fields used here:
///
///   MAP              = Land$English_Channel_1940
///   BriefingNameHtml = &lt;font ...&gt;Mission Title&lt;/font&gt;
///   HourMin          = 930    (09:30 local time; hour*100 + minute)
///
/// A companion &lt;baseName&gt;.briefing file (plain-HTML) may sit beside the
/// .mis file.  When present its text is preferred for the Description field.
///
/// Images in the same folder (or in img/ / images/ subdirectories) are
/// collected as absolute paths and referenced directly — never copied.
/// </summary>
public static class MissionScannerService
{
    private static readonly Regex s_sectionHeader = new(@"^\s*\[([^\]]+)\]\s*$", RegexOptions.Compiled);
    private static readonly Regex s_aircraftClass = new(@"^\s*Class\s+(?:Aircraft\.([^\s;]+)|([^\s;]*Aircraft[^\s;]*))", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Recursively scans <paramref name="folderPath"/> for *.mis files and
    /// yields a <see cref="ScannedMission"/> for each parseable file.
    /// Silently skips files that cannot be read or parsed.
    /// </summary>
    public static IEnumerable<ScannedMission> ScanFolder(
        string          folderPath,
        MissionCategory category     = MissionCategory.Quick,
        string          campaignName = "")
    {
        if (!Directory.Exists(folderPath)) yield break;

        foreach (var file in Directory.EnumerateFiles(
                     folderPath, "*.mis", SearchOption.AllDirectories))
        {
            // Derive the campaign name from the immediate parent subfolder
            // (one level below the root scan folder).
            var derivedCampaign = campaignName;
            if (string.IsNullOrEmpty(derivedCampaign))
            {
                var rel = Path.GetRelativePath(folderPath, Path.GetDirectoryName(file)!);
                derivedCampaign = rel == "." ? "" : rel.Split(Path.DirectorySeparatorChar)[0];
            }

            var entry = TryParse(file, category, derivedCampaign);
            if (entry is not null) yield return entry;
        }
    }

    // ── Internal parsing ──────────────────────────────────────────────────────

    private static ScannedMission? TryParse(
        string          filePath,
        MissionCategory category,
        string          campaignName)
    {
        try
        {
            var lines = File.ReadAllLines(filePath);
            var dir   = Path.GetDirectoryName(filePath)!;
            var stem  = Path.GetFileNameWithoutExtension(filePath);

            var sections = ParseSections(lines);
            var main = ParseSection(lines, "MAIN");

            // ── Title ──────────────────────────────────────────────────────
            var title =
                main.GetValueOrDefault("BriefingNameHtml") ??
                main.GetValueOrDefault("BriefingName")     ??
                main.GetValueOrDefault("Title")            ??
                stem;

            title = StripHtml(title).Trim();
            if (string.IsNullOrWhiteSpace(title)) title = stem;

            // ── Time ───────────────────────────────────────────────────────
            // HourMin is stored as integer: 930 → 09:30, 1400 → 14:00
            string timeDisplay = "—";
            if (main.TryGetValue("HourMin", out var rawTime)
                && int.TryParse(rawTime, out int hm))
            {
                int h = hm / 100, m = hm % 100;
                timeDisplay = $"{h:D2}:{m:D2}";
            }
            else if (main.TryGetValue("TIME", out var rawFloatTime)
                     && double.TryParse(rawFloatTime, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var floatTime))
            {
                var hours = Math.Clamp((int)Math.Floor(floatTime), 0, 23);
                var minutes = Math.Clamp((int)Math.Round((floatTime - hours) * 60), 0, 59);
                timeDisplay = $"{hours:D2}:{minutes:D2}";
            }

            // ── Map ────────────────────────────────────────────────────────
            var map = main.GetValueOrDefault("MAP") ?? "Unknown";
            if (map.Contains('$')) map = map[(map.IndexOf('$') + 1)..];
            map = map.Replace('_', ' ')
                     .Replace("1940", "").Replace("1941", "")
                     .Trim();

            // ── Briefing file ──────────────────────────────────────────────
            // CloD places a <stem>.briefing HTML file beside each .mis file.
            var briefingPath = Path.Combine(dir, stem + ".briefing");
            if (!File.Exists(briefingPath)) briefingPath = null;

            // ── Description ────────────────────────────────────────────────
            // Prefer .briefing file; fall back to [BriefingHtml] section.
            string desc = briefingPath is not null
                ? ParseBriefingFile(briefingPath)
                : ParseBriefingSection(lines);

            // ── Images ─────────────────────────────────────────────────────
            var images = FindImages(dir, stem);
            var routes = ParseRoutes(sections);

            return new ScannedMission(
                filePath, title, timeDisplay, map, desc,
                briefingPath, images, routes, category, campaignName);
        }
        catch
        {
            return null;    // skip unreadable / malformed files
        }
    }

    // ── Image discovery ───────────────────────────────────────────────────────

    private static readonly string[] s_imgExts   = { ".jpg", ".jpeg", ".png", ".bmp" };
    private static readonly string[] s_imgSubdirs = { "img", "images", "image", "pics" };

    private static IReadOnlyList<string> FindImages(string dir, string stem)
    {
        var list = new List<string>();

        // Same-name image beside the .mis file
        foreach (var ext in s_imgExts)
        {
            var p = Path.Combine(dir, stem + ext);
            if (File.Exists(p)) { list.Add(p); break; }
        }

        // Images in common image subdirectories
        foreach (var sub in s_imgSubdirs)
        {
            var subDir = Path.Combine(dir, sub);
            if (!Directory.Exists(subDir)) continue;

            foreach (var ext in s_imgExts)
                list.AddRange(Directory.EnumerateFiles(subDir, "*" + ext,
                                                       SearchOption.TopDirectoryOnly));
        }

        return list;
    }

    // ── INI section parser ────────────────────────────────────────────────────

    private static Dictionary<string, string> ParseSection(string[] lines, string section)
    {
        var  result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        bool active = false;

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.StartsWith('['))
            {
                active = line.Equals($"[{section}]", StringComparison.OrdinalIgnoreCase);
                continue;
            }
            if (!active || line.StartsWith(';')) continue;
            var eq = line.IndexOf('=');
            if (eq < 0) eq = line.IndexOf(' ');
            if (eq > 0) result[line[..eq].Trim()] = line[(eq + 1)..].Trim();
        }

        return result;
    }

    private static Dictionary<string, string> ParseSections(string[] lines)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var current = "";
        var body = new System.Text.StringBuilder();

        foreach (var raw in lines)
        {
            var match = s_sectionHeader.Match(raw);
            if (match.Success)
            {
                if (!string.IsNullOrWhiteSpace(current))
                    result[current] = body.ToString();
                current = match.Groups[1].Value.Trim();
                body.Clear();
                continue;
            }

            if (!string.IsNullOrWhiteSpace(current))
                body.AppendLine(raw);
        }

        if (!string.IsNullOrWhiteSpace(current))
            result[current] = body.ToString();

        return result;
    }

    private static List<ScannedRoute> ParseRoutes(Dictionary<string, string> sections)
    {
        var routes = new List<ScannedRoute>();
        foreach (var (id, body) in sections)
        {
            if (!s_aircraftClass.IsMatch(body)) continue;
            var wayBody = sections.GetValueOrDefault($"{id}_Way") ?? sections.GetValueOrDefault($"{id}.Way") ?? "";
            var waypoints = ParseWaypoints(wayBody);
            if (waypoints.Count < 2) continue;

            routes.Add(new ScannedRoute(id, InferSide(id + " " + body), waypoints));
        }

        return routes;
    }

    private static List<ScannedWaypoint> ParseWaypoints(string body)
    {
        var waypoints = new List<ScannedWaypoint>();
        foreach (var raw in body.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(';')) continue;
            var parts = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) continue;

            var action = parts[0];
            var numeric = parts.Skip(1)
                .Select(p => double.TryParse(p, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : double.NaN)
                .Where(v => !double.IsNaN(v))
                .Take(4)
                .ToArray();
            if (numeric.Length < 2) continue;

            waypoints.Add(new ScannedWaypoint(
                action,
                numeric[0],
                numeric[1],
                numeric.Length > 2 ? numeric[2] : 0,
                numeric.Length > 3 ? numeric[3] : 0));
        }

        return waypoints;
    }

    // ── Briefing text ─────────────────────────────────────────────────────────

    /// <summary>Reads a .briefing (HTML) file and returns stripped plain text.</summary>
    private static string ParseBriefingFile(string briefingPath)
    {
        try
        {
            var html = File.ReadAllText(briefingPath);
            return Truncate(FormatBriefingText(html));
        }
        catch { return string.Empty; }
    }

    /// <summary>Extracts plain text from the optional [BriefingHtml] INI section.</summary>
    private static string ParseBriefingSection(string[] lines)
    {
        bool inSection = false;
        var  sb        = new System.Text.StringBuilder();

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Equals("[BriefingHtml]", StringComparison.OrdinalIgnoreCase))
            { inSection = true; continue; }
            if (line.StartsWith('[') && inSection) break;
            if (!inSection) continue;

            var text = FormatBriefingText(line);
            if (!string.IsNullOrWhiteSpace(text)) sb.Append(text).Append(' ');
            if (sb.Length > 1800) break;
        }

        return Truncate(FormatBriefingText(sb.ToString()));
    }

    private static string Truncate(string s) =>
        s.Length > 1600 ? s[..1597] + "…" : s;

    // ── HTML stripping ────────────────────────────────────────────────────────

    private static readonly Regex s_htmlTag = new(@"<[^>]+>",  RegexOptions.Compiled);
    private static readonly Regex s_htmlEnt = new(@"&[a-z]+;", RegexOptions.Compiled
                                                              | RegexOptions.IgnoreCase);

    private static string StripHtml(string html)
    {
        var s = s_htmlTag.Replace(html, " ");
        s = s_htmlEnt.Replace(s, " ");
        return Regex.Replace(s, @"\s{2,}", " ");
    }

    private static string FormatBriefingText(string value)
    {
        var text = StripHtml(value);
        text = Regex.Replace(text, @"\[(info|mainintro|intro|objective|objectives?|briefing|weather|success|failure|fail|debrief)\]", "\n\n$1\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\[(\d+)\]", "\n\n[$1] ", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"(?:\r?\n\s*){3,}", "\n\n");
        text = Regex.Replace(text, @"[ \t]{2,}", " ");
        return text.Trim();
    }

    private static string InferSide(string text)
    {
        var compact = Regex.Replace(text.ToLowerInvariant(), @"[^a-z0-9]+", "_");
        if (Regex.IsMatch(compact, @"(^|_)ra(_|$)|regia|aeronautica|italian|squadriglia|cr_?42|g_?50|br_?20|fiat|macchi")) return "Regia Aeronautica";
        if (Regex.IsMatch(compact, @"luftwaffe|(^|_)(jg|kg|lg|stg|zg)\d*|staffel|gruppe|bf_?\d|me_?\d|he_?\d|do_?\d|ju_?\d")) return "Luftwaffe";
        if (Regex.IsMatch(compact, @"raf|raaf|sqn|squadron|spitfire|hurricane|blenheim|defiant|beaufighter|gladiator|wellington|kittyhawk|tomahawk")) return "RAF";
        return "Unknown";
    }
}
