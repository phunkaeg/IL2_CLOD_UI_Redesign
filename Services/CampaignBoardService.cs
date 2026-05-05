using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace PlaneLoadoutWpfTest.Services;

public sealed class CampaignBoardCampaign
{
    public string Title { get; init; } = "";
    public string Period { get; init; } = "";
    public string SourceRoot { get; init; } = "";
    public string Description { get; init; } = "";
    public string PosterImage { get; init; } = "";
    public CampaignMapInfo Map { get; init; } = new();
    public List<CampaignDay> Days { get; init; } = [];
    public int MissionCount => Days.Sum(d => d.Missions.Count);
    public string Summary => $"{Days.Count} days / {MissionCount} missions";
}

public sealed class CampaignMapInfo
{
    public string Image { get; init; } = "/Assets/strait_of_dover_map.jpg";
    public int Width { get; init; } = 3993;
    public int Height { get; init; } = 3507;
    public string Theater => Image.Contains("tobruk", StringComparison.OrdinalIgnoreCase)
        ? "Tobruk"
        : "Strait of Dover";
}

public sealed class CampaignDay
{
    public string Date { get; init; } = "";
    public string Title { get; init; } = "";
    public string Summary { get; init; } = "";
    public string Weather { get; init; } = "";
    public List<CampaignMission> Missions { get; init; } = [];
    public string DisplayTitle => string.IsNullOrWhiteSpace(Date) ? Title : $"{Date}  {Title}";
    public string MissionCountText => $"{Missions.Count} missions";
    public string ShortDate => Date.Length >= 10 ? Date[5..] : "";
}

public sealed class CampaignMission
{
    public string Date { get; init; } = "";
    public string Time { get; init; } = "0000";
    public string Title { get; init; } = "";
    public string Type { get; set; } = "Other / Special";
    public string Briefing { get; init; } = "";
    public string Weather { get; init; } = "";
    public CampaignMapInfo Map { get; init; } = new();
    public List<CampaignRole> Playable { get; init; } = [];
    public List<CampaignRoute> Routes { get; init; } = [];
    public List<CampaignSlide> Slides { get; init; } = [];
    public List<string> Images { get; init; } = [];
    public List<CampaignIntelLocation> IntelLocations { get; init; } = [];
    public List<string> SourceFiles { get; init; } = [];
    public string ChapterTitle { get; init; } = "";
    public bool IsLocked { get; init; }
    public string LockStatus => IsLocked ? "LOCKED" : "";
    public string RoleSummary => Playable.Count == 0 ? "No flyable roles detected" : string.Join(", ", Playable.Take(3).Select(r => r.Unit));
    public string RouteSummary => $"{Routes.Count} routes / {Playable.Count} flyable";
    public string DisplayTime => Time.Length == 4 ? $"{Time[..2]}:{Time[2..]}" : Time;
    public string WeatherLabel => string.IsNullOrWhiteSpace(Weather) ? "Weather not specified" : Weather;
    public string ImagePreview => Slides.FirstOrDefault()?.Src ?? Images.FirstOrDefault() ?? "";
    public string PrimarySide => Playable.FirstOrDefault(r => r.Side != "Unknown")?.Side
                                 ?? Routes.FirstOrDefault(r => r.Side != "Unknown")?.Side
                                 ?? "Unknown";
    public string SortieSummary => $"{DisplayTime} / {Type} / {Playable.Count} selectable flights";
}

public sealed class CampaignIntelLocation
{
    public string Name { get; init; } = "";
    public string Kind { get; init; } = "Intel";
    public string Context { get; init; } = "";
}

public sealed class CampaignRole
{
    public string Id { get; init; } = "";
    public string Unit { get; init; } = "";
    public string Side { get; init; } = "Unknown";
    public string Aircraft { get; init; } = "";
    public string Role { get; init; } = "";
    public string Description { get; init; } = "";
    public string Objective { get; init; } = "";
    public string AirGroupId { get; init; } = "";
    public string Display => $"{Unit} - {Aircraft}";
}

public sealed class CampaignRoute
{
    public string Id { get; init; } = "";
    public string AirGroupId { get; init; } = "";
    public string Briefing { get; init; } = "";
    public string Unit { get; init; } = "";
    public string Side { get; init; } = "Unknown";
    public string Aircraft { get; init; } = "";
    public string RawAircraft { get; init; } = "";
    public string Formation { get; init; } = "";
    public string Skill { get; init; } = "";
    public List<CampaignWaypoint> Waypoints { get; init; } = [];
    public string CoordinateSystem { get; init; } = "game";
    public string Display => $"{Briefing} - {Aircraft} ({Waypoints.Count} wp)";
}

public sealed class CampaignWaypoint
{
    public string Action { get; init; } = "";
    public double X { get; init; }
    public double Y { get; init; }
    public double Alt { get; init; }
    public double Speed { get; init; }
}

public sealed class CampaignSlide
{
    public string Section { get; init; } = "";
    public string SectionName { get; init; } = "";
    public string Filename { get; init; } = "";
    public string Src { get; init; } = "";
    public string Caption { get; init; } = "";
    public string Title { get; init; } = "";
}

public static partial class CampaignBoardService
{
    private static readonly string[] s_imageExts = [".jpg", ".jpeg", ".png", ".webp", ".bmp"];
    private static readonly Regex s_fragmentMis = new(@"_\d+\.mis$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex s_nonMissionMis = new(@"^(campaign|mainintro|missionintro|missionsuccess|missionfailure|success|failure)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex s_localisedBriefing = new(@"_(?:pl|ru|cs|de|es|fr|it)\.briefing$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex s_sectionHeader = new(@"^\s*\[([^\]]+)\]\s*$", RegexOptions.Compiled);
    private static readonly Regex s_aircraftClass = new(@"^\s*Class\s+(?:Aircraft\.([^\s;]+)|([^\s;]*Aircraft[^\s;]*))", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex s_narrativeSection = new(@"^(info|mainintro|intro|introduction|success|failure|fail|missionintro|missionsuccess|missionfailure|background|notes?|objectives?|results?|debrief|summary)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex s_unitKeywords = new(@"sqn|squadron|staffel|gruppe|jg|kg|lg|stg|raf|luftwaffe|regia|aeronautica|italian|italia|squadriglia|cr[._-]?42|g[._-]?50|br[._-]?20|fiat|macchi", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private sealed record ParsedMission(CampaignMission Mission, string Date, string DayTitle, string DayWeather);

    public static IReadOnlyList<CampaignBoardCampaign> ScanCampaignRoot(string root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return [];

        var rootIni = ParseCampaignsIni(Path.Combine(root, "campaigns.ini"));
        var folders = Directory.EnumerateDirectories(root).OrderBy(NaturalKey).ToList();

        if (IsSupportedCampaignRoot(root))
            return [BuildCampaign(root, root, rootIni)];

        var campaigns = folders
            .Where(IsSupportedCampaignRoot)
            .Select(d => BuildCampaign(d, d, ParseCampaignsIni(Path.Combine(d, "campaigns.ini"))))
            .Where(c => c.MissionCount > 0)
            .ToList();

        return campaigns;
    }

    private static CampaignBoardCampaign BuildCampaign(string campaignRoot, string sourceRoot, Dictionary<string, Dictionary<string, string>> ini)
    {
        var campaignSection = ini.FirstOrDefault(kvp => !kvp.Key.Contains('\\')).Value;
        var startDate = campaignSection is not null && campaignSection.TryGetValue("Date", out var configuredDate)
            ? configuredDate
            : InferDefaultDate(campaignRoot);

        var parsed = new List<ParsedMission>();
        var dayDirs = Directory.EnumerateDirectories(campaignRoot)
            .Where(d => Regex.IsMatch(Path.GetFileName(d), @"^\d{4}-\d{2}-\d{2}\s*-"))
            .OrderBy(NaturalKey)
            .ToList();

        if (dayDirs.Count > 0)
        {
            foreach (var dayDir in dayDirs)
            {
                var dayName = Path.GetFileName(dayDir);
                var match = Regex.Match(dayName, @"^(\d{4}-\d{2}-\d{2})\s*-\s*(.+)$");
                var date = match.Success ? match.Groups[1].Value : startDate;
                var dayTitle = match.Success ? CleanCampaignTitle(match.Groups[2].Value) : CleanCampaignTitle(dayName);
                AddMissionsFromContainer(dayDir, date, dayTitle, parsed);
            }
        }
        else
        {
            var missionRoot = SelectFidelityRoot(campaignRoot);
            var timeFolders = Directory.EnumerateDirectories(missionRoot)
                .Where(d => Regex.IsMatch(Path.GetFileName(d), @"^\d{3,4}\s*-"))
                .OrderBy(NaturalKey)
                .ToList();

            if (timeFolders.Count > 0)
            {
                foreach (var folder in timeFolders)
                    AddMissionsFromFolder(folder, startDate, DayTitleFromCampaignRoot(campaignRoot), parsed);
            }
            else if (Directory.EnumerateFiles(campaignRoot, "*.mis", SearchOption.TopDirectoryOnly).Any())
            {
                AddMissionsFromFolder(campaignRoot, startDate, DayTitleFromCampaignRoot(campaignRoot), parsed);
            }
            else
            {
                var missionFolders = Directory.EnumerateDirectories(campaignRoot)
                    .Where(ContainsPrimaryMission)
                    .OrderBy(NaturalKey)
                    .ToList();

                for (int i = 0; i < missionFolders.Count; i++)
                {
                    var folder = missionFolders[i];
                    var folderName = Path.GetFileName(folder);
                    var iniSection = FindIniSection(ini, folderName);
                    var missions = ParseMissionFolder(folder, iniSection, startDate);
                    foreach (var mission in missions)
                    {
            var parsedDateTime = ParseBriefingDateTime(mission.Briefing);
            var date = FirstNonEmpty(iniSection?.GetValueOrDefault("Date"), mission.Date, parsedDateTime.Date, DateFromStart(startDate, i));
                        var dayTitle = DayTitleFromFolder(folderName, iniSection?.GetValueOrDefault("Name") ?? mission.Title);
                        parsed.Add(new ParsedMission(mission, date, dayTitle, mission.Weather));
                    }
                }
            }
        }


        var days = parsed
            .GroupBy(p => string.IsNullOrWhiteSpace(p.Date) ? p.DayTitle : p.Date, StringComparer.OrdinalIgnoreCase)
            .Select((group, index) =>
            {
                var missions = group.Select(p => p.Mission).OrderBy(m => m.Time).ThenBy(m => m.Title, StringComparer.OrdinalIgnoreCase).ToList();
                var date = group.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.Date))?.Date ?? "";
                var parsedTitle = group.Select(p => p.DayTitle)
                    .FirstOrDefault(t => !LooksLikeTimeTitle(t) && !LooksLikeTechnicalCampaignTitle(t));
                var title = FirstNonEmpty(parsedTitle, string.IsNullOrWhiteSpace(date) ? $"Day {index + 1}" : "");
                return new CampaignDay
                {
                    Date = date,
                    Title = title,
                    Summary = missions.FirstOrDefault()?.Briefing ?? "",
                    Weather = group.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.DayWeather))?.DayWeather ?? "",
                    Missions = missions
                };
            })
            .OrderBy(d => string.IsNullOrWhiteSpace(d.Date) ? "9999" : d.Date)
            .ThenBy(d => d.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var campaignTitle = campaignSection?.GetValueOrDefault("Name") ?? CleanCampaignTitle(Path.GetFileName(campaignRoot.TrimEnd(Path.DirectorySeparatorChar)));
        var period = MakePeriod(days);
        var map = DetectMap(campaignRoot, days);
        var campaignMeta = ReadCampaignMeta(campaignRoot, days);

        return new CampaignBoardCampaign
        {
            Title = campaignTitle,
            Period = period,
            SourceRoot = sourceRoot,
            Description = campaignMeta.Description,
            PosterImage = campaignMeta.PosterImage,
            Map = map,
            Days = days
        };
    }

    private static (string Description, string PosterImage) ReadCampaignMeta(string campaignRoot, List<CampaignDay> days)
    {
        var briefing = FindCampaignBriefingFile(campaignRoot);
        var sections = briefing is null ? [] : ExtractBriefingSections(ReadTextSafe(briefing), campaignRoot);
        var intro = sections.FirstOrDefault(s => s.Name.Equals("MainIntro", StringComparison.OrdinalIgnoreCase)
                                                 || s.Key.Equals("MainIntro", StringComparison.OrdinalIgnoreCase)
                                                 || s.Name.Equals("Introduction", StringComparison.OrdinalIgnoreCase)
                                                 || s.Key.Equals("Introduction", StringComparison.OrdinalIgnoreCase));
        var description = FirstNonEmpty(
            intro?.Description,
            sections.FirstOrDefault(s => !s.Name.Equals("Title", StringComparison.OrdinalIgnoreCase)
                                         && !s.Key.Equals("Title", StringComparison.OrdinalIgnoreCase)
                                         && !string.IsNullOrWhiteSpace(s.Description))?.Description,
            days.FirstOrDefault(d => !string.IsNullOrWhiteSpace(d.Summary))?.Summary);

        var poster = FindCampaignPoster(campaignRoot, intro?.Slides.FirstOrDefault()?.Src ?? sections.SelectMany(s => s.Slides).FirstOrDefault()?.Src ?? "");
        return (description, poster);
    }

    private static string? FindCampaignBriefingFile(string campaignRoot)
    {
        if (!Directory.Exists(campaignRoot)) return null;
        var folderName = Path.GetFileName(campaignRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var briefings = Directory.EnumerateFiles(campaignRoot, "*.briefing", SearchOption.TopDirectoryOnly)
            .Where(f => !s_localisedBriefing.IsMatch(Path.GetFileName(f)))
            .ToList();

        return briefings
            .OrderByDescending(f => Path.GetFileNameWithoutExtension(f).Equals("Campaign", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(f => Path.GetFileNameWithoutExtension(f).Equals(folderName, StringComparison.OrdinalIgnoreCase))
            .ThenBy(NaturalKey)
            .FirstOrDefault();
    }

    private static string FindCampaignPoster(string campaignRoot, string slideImage)
    {
        if (!Directory.Exists(campaignRoot)) return slideImage;
        var preferred = new[] { "Poster.bmp", "Poster.jpg", "Poster.png", "CampaignMain.jpg", "Campaign.jpg", "Campaign.png" };
        foreach (var name in preferred)
        {
            var path = Path.Combine(campaignRoot, name);
            if (File.Exists(path)) return path;
        }

        return slideImage;
    }

    private static void AddMissionsFromContainer(string dir, string date, string dayTitle, List<ParsedMission> parsed)
    {
        var added = false;
        foreach (var child in Directory.EnumerateDirectories(dir).OrderBy(NaturalKey))
        {
            foreach (var mission in ParseMissionFolder(child, null, date))
            {
                parsed.Add(new ParsedMission(mission, date, dayTitle, mission.Weather));
                added = true;
            }
        }

        if (!added)
        {
            foreach (var mission in ParseMissionFolder(dir, null, date))
                parsed.Add(new ParsedMission(mission, date, dayTitle, mission.Weather));
        }
    }

    private static void AddMissionsFromFolder(string folder, string fallbackDate, string dayTitle, List<ParsedMission> parsed)
    {
        foreach (var mission in ParseMissionFolder(folder, null, fallbackDate))
        {
            var parsedDateTime = ParseBriefingDateTime(mission.Briefing);
            parsed.Add(new ParsedMission(mission, FirstNonEmpty(mission.Date, parsedDateTime.Date, fallbackDate), dayTitle, mission.Weather));
        }
    }

    private static List<CampaignMission> ParseMissionFolder(string folder, Dictionary<string, string>? iniSection, string campaignStartDate)
    {
        var files = Directory.EnumerateFiles(folder, "*.*", SearchOption.TopDirectoryOnly).ToList();
        var misFiles = files
            .Where(f => Path.GetExtension(f).Equals(".mis", StringComparison.OrdinalIgnoreCase))
            .Where(IsMissionFile)
            .OrderBy(NaturalKey)
            .ToList();

        if (misFiles.Count == 0)
        {
            misFiles = files
                .Where(f => Path.GetExtension(f).Equals(".mis", StringComparison.OrdinalIgnoreCase))
                .OrderBy(NaturalKey)
                .ToList();
        }

        if (misFiles.Count == 0) return [];

        var selectedFiles = SelectMissionFiles(folder, misFiles);
        var missions = selectedFiles
            .Select((file, index) => ParseMissionFile(file, files, folder, iniSection, campaignStartDate, index))
            .Where(m => m is not null)
            .Cast<CampaignMission>()
            .ToList();

        if (IsTimeSlotFolder(folder) && missions.Count > 1)
            return [MergeMissionVariants(folder, missions)];

        return DeduplicateMissions(missions);
    }

    private static CampaignMission? ParseMissionFile(string primary, List<string> files, string folder, Dictionary<string, string>? iniSection, string campaignStartDate, int order)
    {
        var text = ReadTextSafe(primary);
        if (string.IsNullOrWhiteSpace(text)) return null;

        var sections = ParseMisSections(text);
        sections.TryGetValue("MAIN", out var mainBody);
        var main = ParseKeyValues(mainBody ?? "");
        var stem = Path.GetFileNameWithoutExtension(primary);
        var briefingFile = files
            .Where(f => Path.GetExtension(f).Equals(".briefing", StringComparison.OrdinalIgnoreCase))
            .Where(f => !s_localisedBriefing.IsMatch(Path.GetFileName(f)))
            .OrderByDescending(f => StripLanguageSuffix(Path.GetFileNameWithoutExtension(f)).Equals(stem, StringComparison.OrdinalIgnoreCase))
            .ThenBy(NaturalKey)
            .FirstOrDefault();

        var briefingSections = briefingFile is null ? [] : ExtractBriefingSections(ReadTextSafe(briefingFile), folder);
        var routes = ParseRoutes(sections);
        var player = main.GetValueOrDefault("player") ?? "";
        var roles = BuildRoles(briefingSections, routes, player);
        var slides = briefingSections.SelectMany(s => s.Slides).ToList();
        var images = Directory.EnumerateFiles(folder, "*.*", SearchOption.TopDirectoryOnly)
            .Where(f => s_imageExts.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .OrderBy(NaturalKey)
            .ToList();

        var title = iniSection?.GetValueOrDefault("Name")
            ?? main.GetValueOrDefault("BriefingName")
            ?? main.GetValueOrDefault("BriefingNameHtml")
            ?? MissionTitleFromFile(primary, folder);
        title = StripMarkup(title);

        var briefing = CleanBriefingText(briefingSections);
        var parsedDateTime = ParseBriefingDateTime(briefing);
        var mainTime = main.TryGetValue("TIME", out var rawTime) ? TimeFloatToHHMM(rawTime) : "";
        var hourMinTime = main.TryGetValue("HourMin", out var hourMin) ? HourMinToHHMM(hourMin) : "";
        var date = FirstNonEmpty(parsedDateTime.Date, DateFromMissionMain(main, campaignStartDate));
        var fileTime = TimeFromFolderOrFile(folder, primary);
        var time = LooksLikeVanillaTimedMissionStem(stem)
            ? FirstNonEmpty(parsedDateTime.Time, fileTime, hourMinTime, mainTime, "0000")
            : FirstNonEmpty(parsedDateTime.Time, hourMinTime, mainTime, fileTime, "0000");
        var weather = ExtractWeather(briefing);
        var map = DetectMissionMap(main.GetValueOrDefault("MAP") ?? "", $"{primary} {title} {briefing}");
        var playable = roles.Count > 0
            ? SelectPlayableRolesForFile(stem, roles)
            : BuildFallbackRoles(briefing, slides, routes, title);

        var mission = new CampaignMission
        {
            Date = date,
            Time = time,
            Title = title,
            Briefing = briefing,
            Weather = weather,
            Map = map,
            Playable = playable,
            Routes = DeduplicateRoutes(routes),
            Slides = slides,
            Images = images,
            IntelLocations = ExtractIntelLocations($"{title} {briefing}"),
            SourceFiles = [primary],
            ChapterTitle = Path.GetFileName(Path.GetDirectoryName(folder) ?? "")
        };

        mission.Type = MissionKindForMission(mission);
        return mission;
    }

    private static CampaignMission MergeMissionVariants(string folder, List<CampaignMission> missions)
    {
        var baseMission = missions.FirstOrDefault(m => m.SourceFiles.Any(f => IsBaseTimeStem(Path.GetFileNameWithoutExtension(f))))
                          ?? missions.First();

        var merged = new CampaignMission
        {
            Date = baseMission.Date,
            Time = baseMission.Time,
            Title = baseMission.Title,
            Briefing = baseMission.Briefing,
            Weather = FirstNonEmpty(baseMission.Weather, missions.Select(m => m.Weather).FirstOrDefault(w => !string.IsNullOrWhiteSpace(w))),
            Map = SelectDominantMap(missions) ?? baseMission.Map,
            Playable = DeduplicateRoles(missions.SelectMany(m => m.Playable)),
            Routes = DeduplicateRoutes(missions.SelectMany(m => m.Routes)),
            Slides = DeduplicateSlides(missions.SelectMany(m => m.Slides)),
            Images = missions.SelectMany(m => m.Images).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(NaturalKey).ToList(),
            IntelLocations = DeduplicateIntelLocations(missions.SelectMany(m => m.IntelLocations)),
            SourceFiles = missions.SelectMany(m => m.SourceFiles).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(NaturalKey).ToList(),
            ChapterTitle = Path.GetFileName(Path.GetDirectoryName(folder) ?? ""),
            IsLocked = missions.All(m => m.IsLocked)
        };

        merged.Type = MissionKindForMission(merged);
        return merged;
    }

    private static List<CampaignRoute> ParseRoutes(Dictionary<string, string> sections)
    {
        var routes = new List<CampaignRoute>();
        foreach (var (id, body) in sections)
        {
            var match = s_aircraftClass.Match(body);
            if (!match.Success) continue;

            var rawAircraft = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value.Split('.').Last();
            var briefing = MatchLine(body, "Briefing") ?? MatchLine(body, "Name") ?? id;
            var formation = MatchLine(body, "Formation") ?? "";
            var skill = MatchLine(body, "Skill") ?? "";
            var wayBody = sections.GetValueOrDefault($"{id}_Way") ?? sections.GetValueOrDefault($"{id}.Way") ?? "";

            routes.Add(new CampaignRoute
            {
                Id = id,
                AirGroupId = id,
                Briefing = StripQuotes(briefing),
                Unit = UnitFromAirGroupId(id, briefing),
                Side = InferSide($"{id} {briefing} {rawAircraft}"),
                Aircraft = PrettyAircraft(rawAircraft),
                RawAircraft = rawAircraft,
                Formation = formation,
                Skill = skill,
                Waypoints = ParseWaypoints(wayBody)
            });
        }

        return routes;
    }

    private static List<CampaignRole> BuildFallbackRoles(string briefing, List<CampaignSlide> slides, List<CampaignRoute> routes, string title)
    {
        var text = string.Join("\n", [briefing, .. slides.Select(s => s.Caption)]);
        var playerAircraft = MatchValue(text, @"Player\s+Plane\s*:\s*([^\r\n]+)");
        playerAircraft = Regex.Replace(playerAircraft, @"\s*Enemy\s+Plane.*$", "", RegexOptions.IgnoreCase).Trim();
        var squadron = Regex.Match(text, @"\b(\d{1,3})\s*(?:\([^)]*\)\s*)?(?:Squadron|Sqn)\b", RegexOptions.IgnoreCase).Groups[1].Value;
        var family = AircraftFamilyToken(playerAircraft);

        CampaignRoute? route = null;
        if (!string.IsNullOrWhiteSpace(family))
            route = routes.FirstOrDefault(r => AircraftFamilyToken(r.Aircraft).Equals(family, StringComparison.OrdinalIgnoreCase));
        if (route is null && !string.IsNullOrWhiteSpace(squadron))
            route = routes.FirstOrDefault(r => NormalToken(r.Unit).Contains(squadron) || NormalToken(r.Briefing).Contains(squadron) || NormalToken(r.Id).Contains(squadron));
        route ??= routes.FirstOrDefault(r => r.Side != "Unknown") ?? routes.FirstOrDefault();

        if (route is null && string.IsNullOrWhiteSpace(playerAircraft) && string.IsNullOrWhiteSpace(squadron)) return [];

        var unit = route?.Unit
                   ?? (!string.IsNullOrWhiteSpace(squadron) ? $"{squadron} Sqn" : "Flight Section");
        var aircraft = !string.IsNullOrWhiteSpace(playerAircraft) ? PrettyAircraft(playerAircraft) : route?.Aircraft ?? "Aircraft TBC";
        var side = InferRoleSide(unit, aircraft, "", "", route?.Side, briefing);
        return
        [
            new CampaignRole
            {
                Id = unit,
                Unit = unit,
                Side = side,
                Aircraft = aircraft,
                Role = MissionKindLabel(title + " " + briefing),
                Description = briefing,
                Objective = FirstSentence(briefing),
                AirGroupId = route?.AirGroupId ?? ""
            }
        ];
    }

    private static List<CampaignRole> BuildRoles(List<BriefingSection> briefingSections, List<CampaignRoute> routes, string player)
    {
        var roles = new List<CampaignRole>();
        var playerPrefix = Regex.Replace(player, @"\.\d+$", "", RegexOptions.IgnoreCase);
        var missionBriefing = CleanBriefingText(briefingSections);

        foreach (var section in briefingSections.Where(s => !s_narrativeSection.IsMatch(s.Key) && !s_narrativeSection.IsMatch(s.Name)))
        {
            var route = FindRouteForBriefingSection(section, routes);
            if (route is null && !s_unitKeywords.IsMatch(section.Description + " " + section.Name + " " + section.Key)) continue;

            roles.Add(new CampaignRole
            {
                Id = section.Key,
                Unit = route?.Unit ?? section.Name,
                Side = InferRoleSide(section.Key, section.Name, route?.Aircraft ?? "", route?.Side, route?.Side, section.Description),
                Aircraft = route?.Aircraft ?? "",
                Role = MissionKindLabel(section.Name + " " + section.Description),
                Description = section.Description,
                Objective = FirstSentence(section.Description),
                AirGroupId = route?.AirGroupId ?? ""
            });
        }

        if (!string.IsNullOrWhiteSpace(playerPrefix))
        {
            foreach (var route in routes.Where(r => r.Id.StartsWith(playerPrefix, StringComparison.OrdinalIgnoreCase)))
            {
                if (roles.Any(r => r.AirGroupId.Equals(route.AirGroupId, StringComparison.OrdinalIgnoreCase))) continue;
                roles.Add(new CampaignRole
                {
                    Id = route.Id,
                    Unit = route.Unit,
                    Side = route.Side,
                    Aircraft = route.Aircraft,
                    Role = MissionKindLabel(route.Briefing + " " + route.Aircraft),
                    Description = missionBriefing,
                    Objective = FirstSentence(missionBriefing),
                    AirGroupId = route.AirGroupId
                });
            }
        }

        return roles;
    }

    private static List<CampaignRole> RestrictPlayableRoles(List<CampaignRole> roles)
    {
        var knownSides = roles
            .Where(r => r.Side != "Unknown")
            .Select(r => r.Side)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (knownSides.Count <= 1) return roles;

        var primarySide = roles.FirstOrDefault(r => r.Side != "Unknown")?.Side;
        return string.IsNullOrWhiteSpace(primarySide)
            ? roles
            : roles.Where(r => r.Side.Equals(primarySide, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private static List<CampaignRole> SelectPlayableRolesForFile(string stem, List<CampaignRole> roles)
    {
        if (IsBaseTimeStem(stem)) return RestrictPlayableRoles(roles);

        var stemFamily = AircraftFamilyToken(stem);
        var unitHints = UnitHintTokens(stem);
        var scored = roles
            .Select(role => (Role: role, Score: VariantRoleScore(stemFamily, unitHints, role)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ToList();

        if (scored.Count == 0) return RestrictPlayableRoles(roles);

        var best = scored[0].Score;
        if (best < 4) return RestrictPlayableRoles(roles);

        return scored
            .Where(x => x.Score == best)
            .Select(x => x.Role)
            .Take(2)
            .ToList();
    }

    private static int VariantRoleScore(string stemFamily, List<string> unitHints, CampaignRole role)
    {
        var score = 0;
        var roleToken = NormalToken($"{role.Id} {role.Unit} {role.AirGroupId} {role.Description}");
        foreach (var hint in unitHints)
        {
            if (roleToken.Contains(hint, StringComparison.OrdinalIgnoreCase))
                score += 8;
        }

        if (!string.IsNullOrWhiteSpace(stemFamily) && AircraftFamilyToken(role.Aircraft).Equals(stemFamily, StringComparison.OrdinalIgnoreCase))
            score += 3;

        return score;
    }

    private static List<string> UnitHintTokens(string stem)
    {
        var hints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in Regex.Matches(stem, @"\b(\d{1,3})\s*(?:sqn|squadron)\b", RegexOptions.IgnoreCase))
        {
            var n = match.Groups[1].Value;
            hints.Add($"{n}sqn");
            hints.Add($"{n}squadron");
            hints.Add($"no{n}squadron");
        }

        foreach (Match match in Regex.Matches(stem, @"\b(\d{1,2})\s*/?\s*((?:JG|KG|LG|StG|ZG)\s*\d+)\b", RegexOptions.IgnoreCase))
        {
            hints.Add(NormalToken(match.Value));
            hints.Add(NormalToken($"{match.Groups[1].Value}/{match.Groups[2].Value}"));
        }

        return hints.ToList();
    }

    private static CampaignRoute? FindRouteForBriefingSection(BriefingSection section, List<CampaignRoute> routes)
    {
        var a = NormalToken(section.Key);
        if (a.Length < 3) return null;
        return routes.FirstOrDefault(r =>
        {
            var b = NormalToken(r.Briefing);
            var c = NormalToken(r.Id);
            return a == b
                   || (b.Length >= 3 && a.Contains(b))
                   || (a.Length >= 3 && b.Contains(a))
                   || (a.Length >= 3 && c.Contains(a));
        });
    }

    private static List<CampaignWaypoint> ParseWaypoints(string body)
    {
        var waypoints = new List<CampaignWaypoint>();
        foreach (var raw in body.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(';')) continue;
            var parts = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) continue;

            var nums = parts.Skip(1)
                .Select(p => double.TryParse(p.TrimEnd(','), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : double.NaN)
                .Where(v => !double.IsNaN(v))
                .ToList();

            if (nums.Count < 2) continue;
            waypoints.Add(new CampaignWaypoint
            {
                Action = parts[0],
                X = nums[0],
                Y = nums[1],
                Alt = nums.Count > 2 ? nums[2] : 0,
                Speed = nums.Count > 3 ? nums[3] : 0
            });
        }

        return waypoints;
    }

    private static List<CampaignRoute> DeduplicateRoutes(IEnumerable<CampaignRoute> routes)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<CampaignRoute>();
        foreach (var route in routes)
        {
            var sig = $"{route.Id}|{route.Aircraft}|{route.Briefing}|{string.Join(';', route.Waypoints.Select(w => $"{Math.Round(w.X)}:{Math.Round(w.Y)}"))}";
            if (seen.Add(sig)) result.Add(route);
        }
        return result;
    }

    private static List<CampaignRole> DeduplicateRoles(IEnumerable<CampaignRole> roles)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<CampaignRole>();
        foreach (var role in roles)
        {
            var sig = $"{NormalToken(role.Unit)}|{NormalToken(role.Aircraft)}|{role.Side}|{NormalToken(role.AirGroupId)}";
            if (seen.Add(sig)) result.Add(role);
        }

        return result;
    }

    private static List<CampaignSlide> DeduplicateSlides(IEnumerable<CampaignSlide> slides)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<CampaignSlide>();
        foreach (var slide in slides)
        {
            var sig = $"{slide.Src}|{slide.SectionName}|{slide.Title}";
            if (seen.Add(sig)) result.Add(slide);
        }

        return result;
    }

    private static List<CampaignIntelLocation> DeduplicateIntelLocations(IEnumerable<CampaignIntelLocation> locations)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<CampaignIntelLocation>();
        foreach (var location in locations)
        {
            if (string.IsNullOrWhiteSpace(location.Name)) continue;
            if (seen.Add($"{NormalToken(location.Name)}|{location.Kind}")) result.Add(location);
        }

        return result;
    }

    private static List<CampaignMission> DeduplicateMissions(IEnumerable<CampaignMission> missions)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<CampaignMission>();
        foreach (var mission in missions)
        {
            var units = string.Join(",", mission.Playable.Select(r => NormalToken(r.Unit)).OrderBy(s => s));
            var sig = $"{mission.Time}|{NormalToken(mission.Title)}|{NormalToken(mission.Briefing)[..Math.Min(80, NormalToken(mission.Briefing).Length)]}|{units}";
            if (seen.Add(sig)) result.Add(mission);
        }

        return result;
    }

    private static Dictionary<string, string> ParseKeyValues(string body)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in body.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(';')) continue;
            var split = line.IndexOf('=');
            if (split < 0) split = line.IndexOf(' ');
            if (split <= 0) continue;
            result[line[..split].Trim()] = StripQuotes(line[(split + 1)..].Trim());
        }
        return result;
    }

    private static Dictionary<string, string> ParseMisSections(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var current = "";
        var body = new StringBuilder();

        foreach (var raw in text.TrimStart('\uFEFF').Split(['\r', '\n'], StringSplitOptions.None))
        {
            var match = s_sectionHeader.Match(raw);
            if (match.Success)
            {
                if (!string.IsNullOrWhiteSpace(current)) result[current] = body.ToString();
                current = match.Groups[1].Value.Trim();
                body.Clear();
            }
            else if (!string.IsNullOrWhiteSpace(current))
            {
                body.AppendLine(raw);
            }
        }

        if (!string.IsNullOrWhiteSpace(current)) result[current] = body.ToString();
        return result;
    }

    private static List<BriefingSection> ExtractBriefingSections(string text, string folder)
    {
        var sections = new List<BriefingSection>();
        foreach (Match match in Regex.Matches(text, @"\[([^\]]+)\]([\s\S]*?)(?=\r?\n\[[^\]]+\]|\s*$)", RegexOptions.IgnoreCase))
        {
            var key = match.Groups[1].Value.Trim();
            var body = match.Groups[2].Value;
            var name = MatchTag(body, "Name") ?? key;
            var description = MatchTag(body, "Description") ?? "";
            var slides = new List<CampaignSlide>();

            foreach (Match slideMatch in Regex.Matches(body, @"<Slide>\s*([^\r\n<]+)\s*(?:\r?\n)?\s*<Caption>\s*([\s\S]*?)(?=<Slide>|\r?\n\[|$)", RegexOptions.IgnoreCase))
            {
                var filename = slideMatch.Groups[1].Value.Trim();
                var caption = StripMarkup(slideMatch.Groups[2].Value.Trim());
                slides.Add(new CampaignSlide
                {
                    Section = key,
                    SectionName = name,
                    Filename = filename,
                    Src = Path.Combine(folder, filename),
                    Caption = caption,
                    Title = string.IsNullOrWhiteSpace(caption) ? name : caption
                });
            }

            sections.Add(new BriefingSection(key, StripMarkup(name), StripMarkup(description), slides));
        }
        return sections;
    }

    private static string? MatchTag(string body, string tag)
    {
        var nextTags = tag.Equals("Description", StringComparison.OrdinalIgnoreCase)
            ? "<Slide>|<Caption>|\\r?\\n\\[|$"
            : "<|$";
        var match = Regex.Match(body, $@"<{tag}>\s*([\s\S]*?)(?={nextTags})", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static Dictionary<string, Dictionary<string, string>> ParseCampaignsIni(string path)
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path)) return result;

        var section = "";
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            var header = s_sectionHeader.Match(line);
            if (header.Success)
            {
                section = header.Groups[1].Value.Trim();
                result.TryAdd(section, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
                continue;
            }

            if (string.IsNullOrWhiteSpace(section) || line.StartsWith(';')) continue;
            var eq = line.IndexOf('=');
            if (eq < 0) eq = line.IndexOf(' ');
            if (eq <= 0) continue;
            var key = line[..eq].Trim();
            if (Regex.IsMatch(key, @"_(pl|ru|de|fr|es|it|cs)$", RegexOptions.IgnoreCase)) continue;
            result[section][key] = line[(eq + 1)..].Trim();
        }

        return result;
    }

    private static Dictionary<string, string>? FindIniSection(Dictionary<string, Dictionary<string, string>> ini, string folderName)
    {
        var folderToken = NormalToken(folderName);
        return ini.FirstOrDefault(kvp => NormalToken(kvp.Key.Split('\\').Last()) == folderToken).Value;
    }

    private static bool ContainsPrimaryMission(string folder)
        => Directory.EnumerateFiles(folder, "*.mis", SearchOption.TopDirectoryOnly).Any(IsMissionFile)
           || Directory.EnumerateFiles(folder, "*.mis", SearchOption.TopDirectoryOnly).Any();

    public static bool IsSupportedCampaignRoot(string folder)
    {
        if (!Directory.Exists(folder)) return false;
        var childDirs = Directory.EnumerateDirectories(folder).ToList();
        if (Directory.EnumerateFiles(folder, "*.mis", SearchOption.TopDirectoryOnly).Any()) return true;
        if (childDirs.Any(d => Regex.IsMatch(Path.GetFileName(d), @"^\d{4}-\d{2}-\d{2}\s*-"))) return true;
        if (childDirs.Any(d => Regex.IsMatch(Path.GetFileName(d), @"^\d+\s*-\s*(high|medium|low|vr|full)", RegexOptions.IgnoreCase))) return true;
        if (childDirs.Any(d => Regex.IsMatch(Path.GetFileName(d), @"^\d{3,4}\s*-"))) return true;
        if (File.Exists(Path.Combine(folder, "campaigns.ini"))) return true;
        return childDirs.Any(ContainsPrimaryMission)
               || childDirs.Any(d => Directory.EnumerateDirectories(d).Any(ContainsPrimaryMission))
               || ContainsCampaignStructureBelow(folder, 4);
    }

    private static bool ContainsCampaignStructureBelow(string folder, int depth)
    {
        if (depth <= 0 || !Directory.Exists(folder)) return false;
        foreach (var child in Directory.EnumerateDirectories(folder))
        {
            var name = Path.GetFileName(child);
            if (Regex.IsMatch(name, @"^\d{4}-\d{2}-\d{2}\s*-", RegexOptions.IgnoreCase)) return true;
            if (Regex.IsMatch(name, @"^\d{3,4}\s*-", RegexOptions.IgnoreCase)) return true;
            if (ContainsPrimaryMission(child)) return true;
            if (ContainsCampaignStructureBelow(child, depth - 1)) return true;
        }

        return false;
    }

    private static bool IsMissionFile(string file)
    {
        var baseName = Path.GetFileNameWithoutExtension(file);
        if (s_fragmentMis.IsMatch(Path.GetFileName(file))) return false;
        return !s_nonMissionMis.IsMatch(baseName);
    }

    private static string SelectFidelityRoot(string campaignRoot)
    {
        var variants = Directory.EnumerateDirectories(campaignRoot)
            .Where(d => Regex.IsMatch(Path.GetFileName(d), @"^\d+\s*-\s*(high|medium|low|vr|full)", RegexOptions.IgnoreCase))
            .ToList();
        if (variants.Count == 0) return campaignRoot;

        var order = new[] { "high", "medium", "low", "vr", "full" };
        return variants
            .OrderBy(d =>
            {
                var name = Path.GetFileName(d).ToLowerInvariant();
                var index = Array.FindIndex(order, name.Contains);
                return index < 0 ? 99 : index;
            })
            .ThenBy(NaturalKey)
            .First();
    }

    private static List<string> SelectMissionFiles(string folder, List<string> files)
    {
        var timePrefix = Regex.Match(Path.GetFileName(folder), @"^\s*(\d{3,4})\b").Groups[1].Value;
        if (!string.IsNullOrWhiteSpace(timePrefix))
        {
            return files
                .OrderByDescending(f => Path.GetFileNameWithoutExtension(f).Equals(timePrefix, StringComparison.OrdinalIgnoreCase))
                .ThenBy(NaturalKey)
                .ToList();
        }

        if (files.Count == 1) return files;
        return files;
    }

    private static bool IsTimeSlotFolder(string folder)
        => Regex.IsMatch(Path.GetFileName(folder), @"^\s*\d{3,4}\b", RegexOptions.IgnoreCase);

    private static bool IsBaseTimeStem(string stem)
        => Regex.IsMatch(stem, @"^\s*\d{3,4}\s*$", RegexOptions.IgnoreCase);

    private static string SelectPrimaryMis(string folder, List<string> files)
    {
        var folderName = Path.GetFileName(folder);
        var timePrefix = Regex.Match(folderName, @"^\s*(\d{3,4})\b").Groups[1].Value;
        if (!string.IsNullOrWhiteSpace(timePrefix))
        {
            var byTime = files.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Equals(timePrefix, StringComparison.OrdinalIgnoreCase));
            if (byTime is not null) return byTime;
        }

        var byName = files.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Equals(folderName, StringComparison.OrdinalIgnoreCase));
        return byName ?? files[0];
    }

    private static CampaignMapInfo DetectMap(string root, IEnumerable<CampaignDay> days)
    {
        var missionMap = SelectDominantMap(days.SelectMany(d => d.Missions));
        if (missionMap is not null) return missionMap;

        var text = root + " " + string.Join(" ", days.SelectMany(d => d.Missions.Select(m => m.Title + " " + m.Briefing)));
        return DetectMissionMap("", text);
    }

    private static CampaignMapInfo? SelectDominantMap(IEnumerable<CampaignMission> missions)
    {
        return missions
            .Select(m => m.Map)
            .Where(m => !string.IsNullOrWhiteSpace(m.Image))
            .GroupBy(m => m.Theater, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .FirstOrDefault();
    }

    private static CampaignMapInfo DetectMissionMap(string declaredMap, string fallbackText)
    {
        if (Regex.IsMatch(declaredMap, @"tobruk|Land\$Tobruk", RegexOptions.IgnoreCase))
            return TobrukMap();

        if (Regex.IsMatch(declaredMap, @"English_Channel|Land\$English", RegexOptions.IgnoreCase))
            return StraitMap();

        if (Regex.IsMatch(fallbackText, @"tobruk|libya|cyrenaica|north.?africa|desert|western.?desert|middle.?east|mediterranean", RegexOptions.IgnoreCase))
        {
            return TobrukMap();
        }

        return StraitMap();
    }

    private static CampaignMapInfo StraitMap()
        => new() { Image = "/Assets/strait_of_dover_map.jpg", Width = 3993, Height = 3507 };

    private static CampaignMapInfo TobrukMap()
        => new() { Image = "/Assets/tobruk_map.jpg", Width = 10000, Height = 9917 };

    private static string MissionKindForMission(CampaignMission mission)
    {
        var text = string.Join(" ", [mission.Type, mission.Title, mission.ChapterTitle, .. mission.Playable.Select(r => $"{r.Role} {r.Aircraft} {r.Description}"), .. mission.Routes.Select(r => $"{r.Briefing} {r.Aircraft}")]);
        return MissionKindLabel(text);
    }

    private static string MissionKindLabel(string text)
    {
        if (Regex.IsMatch(text, @"\bcircus\b", RegexOptions.IgnoreCase)) return "Circus";
        if (Regex.IsMatch(text, @"\brodeo\b", RegexOptions.IgnoreCase)) return "Rodeo";
        if (Regex.IsMatch(text, @"\brhubarb\b", RegexOptions.IgnoreCase)) return "Rhubarb";
        if (Regex.IsMatch(text, @"\bramrod\b", RegexOptions.IgnoreCase)) return "Ramrod";
        if (Regex.IsMatch(text, @"\bnoball\b|no-ball", RegexOptions.IgnoreCase)) return "Noball";
        if (Regex.IsMatch(text, @"rescue|seenot|search", RegexOptions.IgnoreCase)) return "Search & Rescue";
        if (Regex.IsMatch(text, @"recon|recce|aufkl|photo", RegexOptions.IgnoreCase)) return "Reconnaissance";
        if (Regex.IsMatch(text, @"escort|cover|umbrella", RegexOptions.IgnoreCase)) return "Escort / Cover";
        if (Regex.IsMatch(text, @"convoy|shipping|ship|e-boat|boat|barge|harbour|harbor|anchorage|minefield", RegexOptions.IgnoreCase)) return "Anti-shipping";
        if (Regex.IsMatch(text, @"bomb|bomber|bombing|strike|attack|battery|factory|raid|suppression|jabo|ju-88|ju88|blenheim|boston|he-111|he111|do-17|do17|do-217|do217", RegexOptions.IgnoreCase)) return "Bomber / Strike";
        if (Regex.IsMatch(text, @"fighter|intercept|scramble|patrol|sweep|dogfight|furball|cap", RegexOptions.IgnoreCase)) return "Fighter / Patrol";
        if (Regex.IsMatch(text, @"training", RegexOptions.IgnoreCase)) return "Training";
        return "Other / Special";
    }

    private static string InferSide(string text)
    {
        var compact = Regex.Replace(text.ToLowerInvariant(), @"[^a-z0-9]+", "_");
        if (Regex.IsMatch(compact, @"(^|_)ra(_|$)|(^|_)regia(_|$)|aeronautica|italian|italia|squadriglia|gruppo|cr_?42|g_?50|br_?20|cant|z_?1007|mc_?200|fiat|macchi")) return "Regia Aeronautica";
        if (Regex.IsMatch(compact, @"(^|_)lw(_|$)|luftwaffe|(^|_)(jg|kg|lg|stg|zg)\d*|staffel|gruppe|aufkl|erprob|bf_?\d|me_?\d|he_?\d|do_?\d|ju_?\d")) return "Luftwaffe";
        if (Regex.IsMatch(compact, @"(^|_)raf(_|$)|(^|_)raaf(_|$)|sqn|squadron|spitfire|hurricane|blenheim|defiant|beaufighter|gladiator|wellington|kittyhawk|tomahawk|dh82")) return "RAF";
        return "Unknown";
    }

    private static string InferRoleSide(string unit, string name, string aircraft, string? routeSide, string? fallbackSide, string description)
    {
        var unitText = $"{unit} {name}";
        var unitSide = InferSideFromUnit(unitText);
        if (unitSide != "Unknown") return unitSide;

        var aircraftSide = InferSideFromAircraft(aircraft);
        if (aircraftSide != "Unknown") return aircraftSide;

        if (!string.IsNullOrWhiteSpace(routeSide) && routeSide != "Unknown") return routeSide;
        if (!string.IsNullOrWhiteSpace(fallbackSide) && fallbackSide != "Unknown") return fallbackSide;
        return InferSide($"{unitText} {description}");
    }

    private static string InferSideFromUnit(string text)
    {
        var compact = Regex.Replace(text.ToLowerInvariant(), @"[^a-z0-9]+", "_");
        if (Regex.IsMatch(compact, @"(^|_)ra(_|$)|(^|_)regia(_|$)|aeronautica|squadriglia|gruppo")) return "Regia Aeronautica";
        if (Regex.IsMatch(compact, @"(^|_)lw(_|$)|luftwaffe|(^|_)(jg|kg|lg|stg|zg)\d*|staffel|gruppe")) return "Luftwaffe";
        if (Regex.IsMatch(compact, @"(^|_)raf(_|$)|(^|_)raaf(_|$)|(^|_)\d{1,3}sqn(_|$)|sqn|squadron")) return "RAF";
        return "Unknown";
    }

    private static string InferSideFromAircraft(string text)
    {
        var compact = Regex.Replace(text.ToLowerInvariant(), @"[^a-z0-9]+", "_");
        if (Regex.IsMatch(compact, @"cr_?42|g_?50|br_?20|cant|z_?1007|mc_?200|fiat|macchi")) return "Regia Aeronautica";
        if (Regex.IsMatch(compact, @"bf_?\d|me_?\d|he_?\d|do_?\d|ju_?\d")) return "Luftwaffe";
        if (Regex.IsMatch(compact, @"spitfire|hurricane|blenheim|defiant|beaufighter|gladiator|wellington|kittyhawk|tomahawk|dh82")) return "RAF";
        return "Unknown";
    }

    private static string PrettyAircraft(string raw)
    {
        var s = raw.Replace('_', ' ');
        s = Regex.Replace(s, @"-Trop\b", " Trop", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\bCR42\b", "CR.42", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\bG50\b", "G.50", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\bBR20\b", "BR.20", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\bMC200\b", "MC.200", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\bZ1007\b", "Z.1007", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\b(Bf|Me|He|Do|Ju)-", "$1 ", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"HurricaneMkI\b", "Hurricane Mk I", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"SpitfireMkIa\b", "Spitfire Mk Ia", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"SpitfireMkI\b", "Spitfire Mk I", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"BlenheimMkIV\b", "Blenheim Mk IV", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"WellingtonMkIc\b", "Wellington Mk Ic", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"GladiatorMkII\b", "Gladiator Mk II", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"KittyhawkMkIA\b", "Kittyhawk Mk IA", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"TomahawkMkII\b", "Tomahawk Mk II", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"DH82A\b", "DH.82A", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\bBf[ -]?109E[ -]?3\b", "Bf 109E-3", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\bBf[ -]?109E[ -]?4\b", "Bf 109E-4", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"100oct", "100 oct", RegexOptions.IgnoreCase);
        return Regex.Replace(s, @"\s+", " ").Trim();
    }

    private static string AircraftFamilyToken(string value)
    {
        var t = NormalToken(value);
        if (t.Contains("wellington") || t.Contains("vickerswellington")) return "wellington";
        if (t.Contains("spitfire")) return "spitfire";
        if (t.Contains("hurricane")) return "hurricane";
        if (t.Contains("bf109") || t.Contains("me109")) return "109";
        if (t.Contains("bf110") || t.Contains("me110")) return "110";
        if (t.Contains("blenheim")) return "blenheim";
        if (t.Contains("ju87") || t.Contains("stuka")) return "ju87";
        if (t.Contains("ju88")) return "ju88";
        if (t.Contains("cr42")) return "cr42";
        if (t.Contains("g50")) return "g50";
        if (t.Contains("br20")) return "br20";
        if (t.Contains("gladiator")) return "gladiator";
        if (t.Contains("dh82")) return "dh82";
        return t.Length > 10 ? t[..10] : t;
    }

    private static string UnitFromAirGroupId(string id, string briefing)
    {
        var clean = Regex.Replace(id, @"\.\d+$", "", RegexOptions.IgnoreCase);
        foreach (var segment in clean.Split('_'))
        {
            var sqn = Regex.Match(segment, @"(\d+)\s*(?:sqn|squadron|sq\.?)", RegexOptions.IgnoreCase);
            if (sqn.Success) return $"No. {sqn.Groups[1].Value} Squadron";
            var gruppe = Regex.Match(segment, @"(\d+)\s*(?:gruppe?|gr\.?)", RegexOptions.IgnoreCase);
            if (gruppe.Success) return $"{gruppe.Groups[1].Value} Gruppe";
            var staffel = Regex.Match(segment, @"(\d+)\s*(?:staffel|st\.?)", RegexOptions.IgnoreCase);
            if (staffel.Success) return $"{staffel.Groups[1].Value} Staffel";
            var gruppo = Regex.Match(segment, @"(\d+)\s*(?:gruppo|gr\.?)", RegexOptions.IgnoreCase);
            if (gruppo.Success) return $"{gruppo.Groups[1].Value} Gruppo";
            var jg = Regex.Match(segment, @"jg(\d+)", RegexOptions.IgnoreCase);
            if (jg.Success) return $"JG {jg.Groups[1].Value}";
            var kg = Regex.Match(segment, @"kg(\d+)", RegexOptions.IgnoreCase);
            if (kg.Success) return $"KG {kg.Groups[1].Value}";
            var stg = Regex.Match(segment, @"stg(\d+)", RegexOptions.IgnoreCase);
            if (stg.Success) return $"StG {stg.Groups[1].Value}";
        }

        return string.IsNullOrWhiteSpace(briefing) ? id : StripQuotes(briefing);
    }

    private static string CleanBriefingText(List<BriefingSection> sections)
    {
        var info = sections.FirstOrDefault(s => s.Key.Equals("Info", StringComparison.OrdinalIgnoreCase))?.Description;
        var text = FirstNonEmpty(info, sections.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s.Description))?.Description, sections.SelectMany(s => s.Slides).FirstOrDefault()?.Caption);
        return string.IsNullOrWhiteSpace(text) ? "No briefing text detected." : Limit(text, 1200);
    }

    private static string ExtractWeather(string text)
    {
        var match = Regex.Match(text, @"(?:weather|forecast)[:\s-]+([^\r\n.]+(?:\.[^\r\n.]*)?)", RegexOptions.IgnoreCase);
        return match.Success ? Limit(match.Groups[1].Value.Trim(), 180) : "";
    }

    private static List<CampaignIntelLocation> ExtractIntelLocations(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        var result = new List<CampaignIntelLocation>();
        foreach (var location in KnownMapLocationNames())
        {
            foreach (Match match in Regex.Matches(text, $@"\b{Regex.Escape(location)}\b", RegexOptions.IgnoreCase))
            {
                var start = Math.Max(0, match.Index - 90);
                var length = Math.Min(text.Length - start, match.Length + 180);
                var context = StripMarkup(text.Substring(start, length));
                if (!LooksLikeIntelContext(context)) continue;

                result.Add(new CampaignIntelLocation
                {
                    Name = location,
                    Kind = IntelKind(context),
                    Context = Limit(context, 180)
                });
                break;
            }
        }

        return DeduplicateIntelLocations(result);
    }

    private static bool LooksLikeIntelContext(string text)
        => Regex.IsMatch(text, @"\b(enemy|bandits?|plots?|raid|formation|bomber|fighters?|spotted|sighted|detected|reported|target|bomb|attack|intercept|convoy|shipping|harbou?r|airfield|factory|rendezvous)\b",
            RegexOptions.IgnoreCase);

    private static string IntelKind(string text)
    {
        if (Regex.IsMatch(text, @"\b(target|bomb|attack|strike|raid|harbou?r|factory|airfield)\b", RegexOptions.IgnoreCase)) return "Target";
        if (Regex.IsMatch(text, @"\b(enemy|bandits?|plots?|formation|spotted|sighted|detected|reported|intercept)\b", RegexOptions.IgnoreCase)) return "Enemy sighting";
        return "Intel";
    }

    private static List<string> KnownMapLocationNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Hawkinge", "Manston", "Biggin Hill", "North Weald", "Hornchurch", "Tangmere",
            "Kenley", "Croydon", "Ford", "Dover", "Dover Harbour", "Folkestone",
            "Calais-Marck", "Boulogne", "Lympne", "Canterbury", "Ramsgate", "Southend",
            "London", "London centre", "Dunkirk", "Cap Gris-Nez", "Le Havre Octeville",
            "Portsmouth", "Salisbury", "Beachy Head", "Pas de Calais",
            "Tobruk", "Tobruk No5", "Tobruk No3", "Sidi Rezegh", "Derna", "Gazala",
            "Halfaya", "Sollum", "Sidi Barrani", "el Adem", "Akramah",
            "Bi'r al Hakim", "Bir al Hakim", "Gambut"
        };

        var path = Path.Combine(AppContext.BaseDirectory, "map_calibration_data.txt");
        if (!File.Exists(path))
            path = Path.Combine(Directory.GetCurrentDirectory(), "map_calibration_data.txt");

        if (File.Exists(path))
        {
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                var split = line.IndexOf(':');
                if (split <= 0) continue;
                foreach (var name in line[..split].Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    names.Add(Regex.Replace(name, @"\s*\([^)]*\)", "").Trim());
            }
        }

        return names
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .OrderByDescending(n => n.Length)
            .ToList();
    }

    private static string TimeFloatToHHMM(string value)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var f)) return "0000";
        if (f > 0 && f < 1) f *= 24;
        var hours = (int)Math.Floor(f);
        var minutes = (int)Math.Round((f - hours) * 60);
        if (minutes >= 60) { hours++; minutes -= 60; }
        return $"{Math.Clamp(hours, 0, 23):D2}{Math.Clamp(minutes, 0, 59):D2}";
    }

    private static string HourMinToHHMM(string value)
        => int.TryParse(value, out var hm) ? $"{hm / 100:D2}{hm % 100:D2}" : "0000";

    private static string DateFromStart(string startDate, int index)
        => DateTime.TryParse(startDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var start)
            ? start.AddDays(index).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : "";

    private static string DateFromMissionMain(Dictionary<string, string> main, string campaignStartDate)
    {
        if (!main.TryGetValue("DayOffset", out var rawOffset)) return "";
        if (!int.TryParse(rawOffset, NumberStyles.Integer, CultureInfo.InvariantCulture, out var offset)) return "";
        return DateFromStart(campaignStartDate, offset);
    }

    private static string InferDefaultDate(string root)
    {
        var name = Path.GetFileName(root);
        if (Regex.IsMatch(name, @"jubilee|dieppe", RegexOptions.IgnoreCase)) return "1942-08-19";
        if (Regex.IsMatch(root, @"tobruk|desert", RegexOptions.IgnoreCase)) return "1941-06-01";
        return "1940-07-10";
    }

    private static (string Date, string Time) ParseBriefingDateTime(string text)
    {
        var time = "";
        var timeMatch = Regex.Match(text, @"\bTime\s*[:=-]\s*([01]\d|2[0-3])[:.h ]?([0-5]\d)\b", RegexOptions.IgnoreCase);
        if (!timeMatch.Success) timeMatch = Regex.Match(text, @"\bMission\s+Time\s*[:=-]\s*([01]\d|2[0-3])[:.h ]?([0-5]\d)\b", RegexOptions.IgnoreCase);
        if (!timeMatch.Success) timeMatch = Regex.Match(text, @"\b(\d{4})\s*(?:hrs?|hours?)\b", RegexOptions.IgnoreCase);
        if (!timeMatch.Success) timeMatch = Regex.Match(text, @"\bat\s+(\d{1,2})[:.h](\d{2})", RegexOptions.IgnoreCase);
        if (!timeMatch.Success) timeMatch = Regex.Match(text, @"\b(\d{1,2})[:.h](\d{2})\s*(?:hrs?|am|pm)?", RegexOptions.IgnoreCase);
        if (timeMatch.Success)
        {
            time = timeMatch.Groups.Count > 2 && timeMatch.Groups[2].Success
                ? $"{int.Parse(timeMatch.Groups[1].Value, CultureInfo.InvariantCulture):D2}{int.Parse(timeMatch.Groups[2].Value, CultureInfo.InvariantCulture):D2}"
                : timeMatch.Groups[1].Value;
        }

        var date = "";
        var iso = Regex.Match(text, @"\b(\d{4})-(\d{2})-(\d{2})\b");
        if (iso.Success) date = iso.Value;
        else
        {
            var slashDate = Regex.Match(text, @"\b(\d{1,2})/(\d{1,2})/(\d{4})\b", RegexOptions.IgnoreCase);
            if (slashDate.Success)
            {
                date = $"{slashDate.Groups[3].Value}-{int.Parse(slashDate.Groups[2].Value, CultureInfo.InvariantCulture):D2}-{int.Parse(slashDate.Groups[1].Value, CultureInfo.InvariantCulture):D2}";
            }

            if (string.IsNullOrWhiteSpace(date))
            {
                var longDate = Regex.Match(text, @"\b(\d{1,2})(?:st|nd|rd|th)?(?:\s+of)?\s+([A-Za-z]+),?\s+(\d{4})\b", RegexOptions.IgnoreCase);
                if (longDate.Success && TryMonth(longDate.Groups[2].Value, out var month))
                {
                    date = $"{longDate.Groups[3].Value}-{month:D2}-{int.Parse(longDate.Groups[1].Value, CultureInfo.InvariantCulture):D2}";
                }
            }

            if (string.IsNullOrWhiteSpace(date))
            {
                var monthFirstDate = Regex.Match(text, @"\b([A-Za-z]+)\s+(\d{1,2})(?:st|nd|rd|th)?,?\s+(\d{4})\b", RegexOptions.IgnoreCase);
                if (monthFirstDate.Success && TryMonth(monthFirstDate.Groups[1].Value, out var month))
                {
                    date = $"{monthFirstDate.Groups[3].Value}-{month:D2}-{int.Parse(monthFirstDate.Groups[2].Value, CultureInfo.InvariantCulture):D2}";
                }
            }
        }

        return (date, time);
    }

    private static bool TryMonth(string value, out int month)
    {
        month = Array.FindIndex(CultureInfo.InvariantCulture.DateTimeFormat.MonthNames,
            m => m.StartsWith(value, StringComparison.OrdinalIgnoreCase)) + 1;
        return month > 0;
    }

    private static string DayTitleFromFolder(string folderName, string fallback)
    {
        var title = CleanFolderTitle(folderName);
        title = Regex.Replace(title, @"^\d{3,4}\s*[-_. ]\s*", "").Trim();
        return string.IsNullOrWhiteSpace(title) ? fallback : title;
    }

    private static string DayTitleFromCampaignRoot(string campaignRoot)
    {
        var name = Path.GetFileName(campaignRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return LooksLikeTechnicalCampaignTitle(name) ? "" : CleanCampaignTitle(name);
    }

    private static bool LooksLikeTechnicalCampaignTitle(string value)
        => Regex.IsMatch(value, @"^\s*campaign(?:[_-]?(?:de|gb|it|\d+)(?:[_-]\d+)?)?\s*$", RegexOptions.IgnoreCase);

    private static string MissionTitleFromFile(string file, string folder)
    {
        var folderName = Path.GetFileName(folder);
        var fileName = Path.GetFileNameWithoutExtension(file);
        if (Regex.IsMatch(folderName, @"^\d{3,4}\s*-", RegexOptions.IgnoreCase)) return CleanCampaignTitle(folderName);
        return CleanCampaignTitle(fileName);
    }

    private static string TimeFromFolderOrFile(string folder, string file)
    {
        var text = $"{Path.GetFileName(folder)} {Path.GetFileNameWithoutExtension(file)}";
        var match = Regex.Match(text, @"\b([01]?\d|2[0-3])[:.h]([0-5]\d)\b");
        if (match.Success) return $"{int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture):D2}{int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture):D2}";
        match = Regex.Match(text, @"\b([01]\d|2[0-3])([0-5]\d)\b");
        return match.Success ? match.Value : "";
    }

    private static bool LooksLikeVanillaTimedMissionStem(string stem)
        => Regex.IsMatch(stem, @"^\s*(?:\d{2,3}|bf|me|ju|he|do|spitfire|hurricane|blenheim|wellington)[\s_-]+(?:[01]\d|2[0-3])[0-5]\d\b",
            RegexOptions.IgnoreCase);

    private static bool LooksLikeTimeTitle(string value)
        => Regex.IsMatch(value, @"^\s*\d{3,4}\b");

    private static string MakePeriod(List<CampaignDay> days)
    {
        var dates = days.Select(d => d.Date).Where(d => !string.IsNullOrWhiteSpace(d)).ToList();
        return dates.Count == 0 ? "" : $"{dates.First()} to {dates.Last()}";
    }

    private static string MatchLine(string body, string key)
    {
        var match = Regex.Match(body, $@"^\s*{Regex.Escape(key)}\s+""?(.+?)""?\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value.Trim() : "";
    }

    private static string MatchValue(string text, string pattern)
    {
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : "";
    }

    private static string ReadTextSafe(string path)
    {
        try { return File.ReadAllText(path); }
        catch { return ""; }
    }

    private static string NaturalKey(string path)
        => Regex.Replace(Path.GetFileName(path), @"\d+", m => m.Value.PadLeft(10, '0'));

    private static string CleanFolderTitle(string name)
        => Regex.Replace(name, @"^\s*\d+\s*[-_. ]\s*", "").Replace('_', ' ').Trim();

    private static string CleanCampaignTitle(string name)
        => Regex.Replace(name, @"^\d{4}-\d{2}-\d{2}\s*-\s*", "", RegexOptions.IgnoreCase)
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Trim();

    private static string NormalToken(string value)
        => Regex.Replace(value.ToLowerInvariant(), @"[^a-z0-9]+", "");

    private static string StripQuotes(string value)
        => value.Trim().Trim('"');

    private static string StripLanguageSuffix(string value)
        => Regex.Replace(value, @"_(?:pl|ru|cs|de|es|fr|it)$", "", RegexOptions.IgnoreCase);

    private static string StripMarkup(string value)
    {
        var s = Regex.Replace(value, @"<[^>]+>", " ");
        s = Regex.Replace(s, @"&[a-z]+;", " ", RegexOptions.IgnoreCase);
        return Regex.Replace(s, @"\s+", " ").Trim();
    }

    private static string FirstSentence(string text)
    {
        var clean = StripMarkup(text);
        var idx = clean.IndexOf('.');
        return idx > 0 ? clean[..Math.Min(idx + 1, 180)] : Limit(clean, 180);
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "";

    private static string Limit(string value, int max)
        => value.Length <= max ? value : value[..Math.Max(0, max - 1)] + "…";

    private sealed record BriefingSection(string Key, string Name, string Description, List<CampaignSlide> Slides);
}
