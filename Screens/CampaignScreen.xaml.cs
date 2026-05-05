using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using PlaneLoadoutWpfTest.Services;

namespace PlaneLoadoutWpfTest.Screens;

public sealed class CampaignTimelineItem
{
    public string Key { get; init; } = "";
    public string Label { get; init; } = "";
    public string Subtitle { get; init; } = "";
    public string Title { get; init; } = "";
    public string CountText { get; init; } = "";
    public CampaignDay? Day { get; init; }
    public List<CampaignMission> Missions { get; init; } = [];
}

public partial class CampaignScreen : UserControl
{
    private enum TimelineMode { Days, Times }

    private readonly string _selectedRoot;
    private CampaignBoardCampaign? _preloadedCampaign;
    private readonly List<CampaignBoardCampaign> _campaigns = [];
    private readonly List<CampaignTimelineItem> _timelineItems = [];
    private readonly List<GalleryItem> _galleryItems = [];
    private bool _refreshingFilters;
    private CampaignBoardCampaign? _selectedCampaign;
    private CampaignDay? _selectedDay;
    private CampaignMission? _selectedMission;
    private CampaignRole? _selectedRole;
    private List<CampaignRoute> _displayRoutes = [];
    private TimelineMode _timelineMode = TimelineMode.Days;
    private bool _mapDragging;
    private Point _mapDragStart;
    private Point _mapTranslateStart;
    private int _galleryIndex;
    private List<Point> _lastRoutePoints = [];
    private double _mapZoom = 1;
    private Point _mapPan;
    private double _mapNativeWidth = 3993;
    private double _mapNativeHeight = 3507;
    private string _currentMapImage = "";
    private bool _unlockCampaignsForTesting;
    private DispatcherTimer? _mapAnimationTimer;
    private static readonly Lazy<IReadOnlyDictionary<string, Point>> s_mapCalibrationPixels = new(LoadMapCalibrationPixels);

    private sealed record GalleryItem(string Image, string Title, string Caption);

    public CampaignScreen()
        : this("", null)
    {
    }

    public CampaignScreen(string selectedRoot, CampaignBoardCampaign? preloadedCampaign = null)
    {
        _selectedRoot = selectedRoot;
        _preloadedCampaign = preloadedCampaign;
        InitializeComponent();
        PilotResultCombo.ItemsSource = new[] { "Completed", "Partial success", "Failed", "Aborted", "Missing" };
        PilotLandingCombo.ItemsSource = new[] { "Returned to base", "Diverted", "Force landed", "Bailed out", "Killed / missing" };
        PilotResultCombo.SelectedIndex = 0;
        PilotLandingCombo.SelectedIndex = 0;
        Loaded += async (_, _) => await LoadCampaignsAsync();
    }

    private async Task LoadCampaignsAsync()
    {
        _campaigns.Clear();

        var settings = AppSettingsService.Current;
        var scanRoot = FirstNonEmpty(
            _selectedRoot,
            MissionPathResolver.ExistingPaths(MissionPathResolver.CampaignRoots(settings)).FirstOrDefault(),
            MissionPathResolver.ExistingEntries(MissionPathResolver.SingleMissionRoots(settings)).FirstOrDefault()?.Path);
        SourcePathText.Text = string.IsNullOrWhiteSpace(scanRoot)
            ? "Set the Cliffs of Dover install folder or Documents root in the cog menu."
            : scanRoot;

        CampaignTitleText.Text = "CAMPAIGN BOARD";
        CampaignStatusText.Text = "Parsing campaign data...";
        UpdateHeaderWidgets();
        SetLoading(true);

        IReadOnlyList<CampaignBoardCampaign> campaigns = [];
        var parseFailed = false;
        try
        {
            if (_preloadedCampaign is not null)
            {
                campaigns = [_preloadedCampaign];
                _preloadedCampaign = null;
            }
            else
            {
                campaigns = await Task.Run(() => CampaignBoardService.ScanCampaignRoot(scanRoot));
            }
        }
        catch (Exception ex)
        {
            parseFailed = true;
            CampaignStatusText.Text = $"Campaign parsing failed: {ex.Message}";
        }
        finally
        {
            SetLoading(false);
        }

        if (parseFailed) return;

        _campaigns.AddRange(campaigns);
        CampaignBoardSession.Remember(_campaigns);
        CampaignList.ItemsSource = _campaigns;
        CampaignStatusText.Text = _campaigns.Count == 0
            ? "No campaign folders detected"
            : $"{_campaigns.Count} campaigns detected";

        if (_campaigns.Count > 0)
        {
            CampaignTitleText.Text = _campaigns[0].Title;
            CampaignList.SelectedItem = null;
            CampaignList.SelectedIndex = 0;
        }
        else
        {
            DayList.ItemsSource = null;
            MissionList.ItemsSource = null;
            ClearDetails();
            RefreshFilters();
        }
    }

    private void CampaignList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedCampaign = CampaignList.SelectedItem as CampaignBoardCampaign;
        _selectedDay = null;
        _selectedMission = null;
        _selectedRole = null;
        CampaignTitleText.Text = string.IsNullOrWhiteSpace(_selectedCampaign?.Title)
            ? "CAMPAIGN BOARD"
            : _selectedCampaign.Title;
        ApplyMapForMission(null);
        UpdateHeaderWidgets();
        TimelineHeaderText.Text = string.IsNullOrWhiteSpace(_selectedCampaign?.Period)
            ? "CAMPAIGN TIMELINE"
            : $"CAMPAIGN TIMELINE / {_selectedCampaign.Period}";

        RefreshFilters();
        RebuildTimeline(true);
    }

    private void DayList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var item = DayList.SelectedItem as CampaignTimelineItem;
        _selectedDay = item?.Day ?? MakeTimelineDay(item);
        if (_selectedDay is not null)
        {
            SelectedDayText.Text = string.IsNullOrWhiteSpace(_selectedDay.Date)
                ? _selectedDay.Title
                : string.IsNullOrWhiteSpace(_selectedDay.Title) ? _selectedDay.Date : $"{_selectedDay.Date} / {_selectedDay.Title}";
            CenterTimelineItem(item);
        }
        ApplyMissionFilters();
        UpdateHeaderWidgets();
    }

    private void MissionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => ShowMission(MissionList.SelectedItem as CampaignMission);

    private void RoleList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedRole = RoleList.SelectedItem as CampaignRole;
        RefreshSelectedFlightRoute();
        UpdateHeaderWidgets();
    }

    private void Filter_Changed(object sender, RoutedEventArgs e)
    {
        if (_refreshingFilters) return;
        if (sender == SideFilterCombo)
        {
            RefreshDependentFilters();
        }
        RebuildTimeline(false);
        UpdateHeaderWidgets();
    }

    private async void Rescan_Click(object sender, RoutedEventArgs e)
        => await LoadCampaignsAsync();

    private void ExportJson_Click(object sender, RoutedEventArgs e)
        => ExportParsedCampaignJson();

    public void ExportParsedCampaignJson()
    {
        if (_campaigns.Count == 0)
        {
            CampaignStatusText.Text = "No parsed campaign data to export.";
            return;
        }

        var path = PickExportPath("campaign-board-export.json", "JSON files (*.json)|*.json");
        if (string.IsNullOrWhiteSpace(path)) return;

        CampaignBoardSession.Remember(_campaigns);
        File.WriteAllText(path, CampaignBoardExportService.ToJson(_campaigns));
        CampaignStatusText.Text = $"Exported parsed campaign JSON: {path}";
    }

    private void ExportDiagnostics_Click(object sender, RoutedEventArgs e)
        => ExportParserDiagnostics();

    public void ExportParserDiagnostics()
    {
        if (_campaigns.Count == 0)
        {
            CampaignStatusText.Text = "No parsed campaign data to report.";
            return;
        }

        var path = PickExportPath("campaign-parser-diagnostics.md", "Markdown files (*.md)|*.md");
        if (string.IsNullOrWhiteSpace(path)) return;

        CampaignBoardSession.Remember(_campaigns);
        File.WriteAllText(path, CampaignBoardExportService.BuildDiagnostics(_campaigns));
        CampaignStatusText.Text = $"Wrote parser diagnostics: {path}";
    }

    private void UnlockCampaigns_Click(object sender, RoutedEventArgs e)
    {
        _unlockCampaignsForTesting = !_unlockCampaignsForTesting;
        UnlockCampaignsButton.Content = _unlockCampaignsForTesting ? "CAMPAIGNS UNLOCKED" : "UNLOCK CAMPAIGNS";
        UnlockCampaignsButton.Style = _unlockCampaignsForTesting
            ? (Style)FindResource("OkButton")
            : (Style)FindResource(typeof(Button));
        ShowMission(_selectedMission);
    }

    private static string? PickExportPath(string fileName, string filter)
    {
        var dialog = new SaveFileDialog
        {
            FileName = fileName,
            Filter = filter,
            AddExtension = true,
            OverwritePrompt = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private void RouteCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        => DrawRoutes();

    private void TimelineDays_Click(object sender, RoutedEventArgs e)
    {
        _timelineMode = TimelineMode.Days;
        RebuildTimeline(true);
    }

    private void TimelineTimes_Click(object sender, RoutedEventArgs e)
    {
        _timelineMode = TimelineMode.Times;
        RebuildTimeline(true);
    }

    private void DayList_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var scrollViewer = FindVisualChild<ScrollViewer>(DayList);
        if (scrollViewer is null) return;

        scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - e.Delta);
        e.Handled = true;
    }

    private void SmoothVerticalList_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not DependencyObject source) return;
        var scrollViewer = FindVisualChild<ScrollViewer>(source);
        if (scrollViewer is null) return;

        var target = Math.Clamp(
            scrollViewer.VerticalOffset - e.Delta * 0.42,
            0,
            scrollViewer.ScrollableHeight);
        AnimateVerticalOffset(scrollViewer, target);
        e.Handled = true;
    }

    private static void AnimateVerticalOffset(ScrollViewer scrollViewer, double target)
    {
        var start = scrollViewer.VerticalOffset;
        var distance = target - start;
        if (Math.Abs(distance) < 1)
        {
            scrollViewer.ScrollToVerticalOffset(target);
            return;
        }

        var started = DateTime.UtcNow;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += (_, _) =>
        {
            var progress = Math.Min(1, (DateTime.UtcNow - started).TotalMilliseconds / 130);
            var eased = 1 - Math.Pow(1 - progress, 3);
            scrollViewer.ScrollToVerticalOffset(start + distance * eased);
            if (progress < 1) return;
            scrollViewer.ScrollToVerticalOffset(target);
            timer.Stop();
        };
        timer.Start();
    }

    private void SetLoading(bool isLoading)
    {
        LoadingOverlay.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
        LoadingOverlayText.Text = isLoading
            ? "Scanning campaigns, briefings, routes and images..."
            : "";
    }

    private void CenterTimelineItem(CampaignTimelineItem? item)
    {
        if (item is null) return;

        Dispatcher.BeginInvoke(() =>
        {
            var index = DayList.Items.IndexOf(item);
            var scrollViewer = FindVisualChild<ScrollViewer>(DayList);
            if (index < 0 || scrollViewer is null)
            {
                DayList.ScrollIntoView(item);
                return;
            }

            const double itemWidth = 262;
            var target = index * itemWidth - (scrollViewer.ViewportWidth - itemWidth) / 2;
            AnimateTimelineOffset(scrollViewer, Math.Max(0, target));
        });
    }

    private static void AnimateTimelineOffset(ScrollViewer scrollViewer, double target)
    {
        var start = scrollViewer.HorizontalOffset;
        var distance = target - start;
        if (Math.Abs(distance) < 1)
        {
            scrollViewer.ScrollToHorizontalOffset(target);
            return;
        }

        var started = DateTime.UtcNow;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += (_, _) =>
        {
            var progress = Math.Min(1, (DateTime.UtcNow - started).TotalMilliseconds / 260);
            var eased = 1 - Math.Pow(1 - progress, 3);
            scrollViewer.ScrollToHorizontalOffset(start + distance * eased);
            if (progress < 1) return;
            scrollViewer.ScrollToHorizontalOffset(target);
            timer.Stop();
        };
        timer.Start();
    }

    private void Back_Click(object sender, RoutedEventArgs e)
        => NavigationService.Back();

    private void RefreshFilters()
    {
        _refreshingFilters = true;
        var currentSide = SideFilterCombo.SelectedItem as string ?? "All sides";
        var currentType = TypeFilterCombo.SelectedItem as string ?? "All types";
        var currentGroup = GroupFilterCombo.SelectedItem as string ?? "All units";
        var currentAircraft = AircraftFilterCombo.SelectedItem as string ?? "All aircraft";
        var currentMap = MapFilterCombo.SelectedItem as string ?? "All maps";

        var missions = _selectedCampaign?.Days.SelectMany(d => d.Missions).ToList() ?? [];
        var mapTheaters = missions
            .Select(m => m.Map.Theater)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var sides = missions
            .SelectMany(m => m.Playable.Select(r => r.Side))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s)
            .ToList();

        var types = missions
            .Select(m => m.Type)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s)
            .ToList();

        SideFilterCombo.ItemsSource = new[] { "All sides" }.Concat(sides).ToList();
        TypeFilterCombo.ItemsSource = new[] { "All types" }.Concat(types).ToList();
        MapFilterCombo.ItemsSource = new[] { "All maps" }.Concat(mapTheaters).ToList();
        SideFilterCombo.SelectedItem = SideFilterCombo.Items.Contains(currentSide) ? currentSide : "All sides";
        TypeFilterCombo.SelectedItem = TypeFilterCombo.Items.Contains(currentType) ? currentType : "All types";
        MapFilterCombo.SelectedItem = MapFilterCombo.Items.Contains(currentMap) ? currentMap : "All maps";
        RefreshDependentFilters(currentGroup, currentAircraft);
        _refreshingFilters = false;
    }

    private void RefreshDependentFilters(string? preferredGroup = null, string? preferredAircraft = null)
    {
        var side = SideFilterCombo.SelectedItem as string ?? "All sides";
        var missions = _selectedCampaign?.Days.SelectMany(d => d.Missions).ToList() ?? [];

        var groups = missions
            .SelectMany(m => m.Playable.Select(r => (Name: FirstNonEmpty(r.Unit, r.Id), r.Side)))
            .Where(x => !string.IsNullOrWhiteSpace(x.Name) && SideMatches(x.Side, side))
            .Select(x => x.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var aircraft = missions
            .SelectMany(m => m.Playable.Select(r => (Name: r.Aircraft, r.Side)))
            .Where(x => !string.IsNullOrWhiteSpace(x.Name) && SideMatches(x.Side, side))
            .Select(x => x.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();

        preferredGroup ??= GroupFilterCombo.SelectedItem as string ?? "All units";
        preferredAircraft ??= AircraftFilterCombo.SelectedItem as string ?? "All aircraft";

        GroupFilterCombo.ItemsSource = new[] { "All units" }.Concat(groups).ToList();
        AircraftFilterCombo.ItemsSource = new[] { "All aircraft" }.Concat(aircraft).ToList();
        GroupFilterCombo.SelectedItem = GroupFilterCombo.Items.Contains(preferredGroup) ? preferredGroup : "All units";
        AircraftFilterCombo.SelectedItem = AircraftFilterCombo.Items.Contains(preferredAircraft) ? preferredAircraft : "All aircraft";
    }

    private void RebuildTimeline(bool resetSelection)
    {
        if (_refreshingFilters || _selectedCampaign is null)
        {
            return;
        }

        _timelineItems.Clear();
        var previousKey = (DayList.SelectedItem as CampaignTimelineItem)?.Key;

        if (_timelineMode == TimelineMode.Days)
        {
            TimelineDaysButton.Style = (Style)FindResource("OkButton");
            TimelineTimesButton.Style = (Style)FindResource(typeof(Button));

            foreach (var day in _selectedCampaign.Days)
            {
                var matching = day.Missions.Where(MissionPassesFilters).ToList();
                if (HasActiveMissionFilters() && matching.Count == 0) continue;

                var dayIndex = _selectedCampaign.Days.IndexOf(day) + 1;
                _timelineItems.Add(new CampaignTimelineItem
                {
                    Key = "day:" + day.Date + ":" + day.Title,
                    Label = $"Day {dayIndex}",
                    Subtitle = day.ShortDate,
                    Title = FirstNonEmpty(day.Title, day.Date),
                    CountText = $"{matching.Count} / {day.Missions.Count} missions",
                    Day = day,
                    Missions = matching
                });
            }
        }
        else
        {
            TimelineDaysButton.Style = (Style)FindResource(typeof(Button));
            TimelineTimesButton.Style = (Style)FindResource("OkButton");

            foreach (var group in _selectedCampaign.Days
                         .SelectMany(d => d.Missions.Select(m => (Day: d, Mission: m)))
                         .Where(x => MissionPassesFilters(x.Mission))
                         .GroupBy(x => x.Mission.Time)
                         .OrderBy(g => g.Key))
            {
                var missions = group.Select(x => x.Mission).OrderBy(m => m.Title, StringComparer.OrdinalIgnoreCase).ToList();
                var firstDay = group.Select(x => x.Day).FirstOrDefault();
                _timelineItems.Add(new CampaignTimelineItem
                {
                    Key = "time:" + group.Key,
                    Label = "Time slot",
                    Subtitle = missions.FirstOrDefault()?.DisplayTime ?? group.Key,
                    Title = $"{missions.FirstOrDefault()?.DisplayTime ?? group.Key} operations",
                    CountText = $"{missions.Count} missions",
                    Day = null,
                    Missions = missions
                });
            }
        }

        DayList.ItemsSource = null;
        DayList.ItemsSource = _timelineItems;

        if (_timelineItems.Count == 0)
        {
            _selectedDay = null;
            MissionList.ItemsSource = null;
            ClearDetails();
            return;
        }

        if (!resetSelection && !string.IsNullOrWhiteSpace(previousKey))
        {
            var existing = _timelineItems.FirstOrDefault(i => i.Key.Equals(previousKey, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                DayList.SelectedItem = existing;
                return;
            }
        }

        DayList.SelectedIndex = 0;
    }

    private CampaignDay? MakeTimelineDay(CampaignTimelineItem? item)
    {
        if (item is null) return null;
        if (item.Day is not null) return item.Day;

        return new CampaignDay
        {
            Date = "",
            Title = item.Title,
            Summary = item.Missions.FirstOrDefault()?.Briefing ?? "",
            Weather = item.Missions.FirstOrDefault()?.Weather ?? "",
            Missions = item.Missions
        };
    }

    private void ApplyMissionFilters()
    {
        if (_selectedDay is null)
        {
            MissionList.ItemsSource = null;
            ClearDetails();
            return;
        }

        var missions = _selectedDay.Missions.Where(MissionPassesFilters).ToList();

        MissionList.ItemsSource = missions;
        if (missions.Count > 0)
            MissionList.SelectedIndex = 0;
        else
            ClearDetails();
    }

    private bool MissionPassesFilters(CampaignMission m)
    {
        var query = SearchBox.Text.Trim().ToLowerInvariant();
        var side = SideFilterCombo.SelectedItem as string ?? "All sides";
        var type = TypeFilterCombo.SelectedItem as string ?? "All types";
        var group = GroupFilterCombo.SelectedItem as string ?? "All units";
        var aircraft = AircraftFilterCombo.SelectedItem as string ?? "All aircraft";
        var map = MapFilterCombo.SelectedItem as string ?? "All maps";

        if (!MapMatches(m, map)) return false;
        if (type != "All types" && !m.Type.Equals(type, StringComparison.OrdinalIgnoreCase)) return false;

        var matchingRoles = m.Playable.Where(r => RoleMatches(r, side, group, aircraft)).ToList();
        if (matchingRoles.Count == 0) return false;

        if (string.IsNullOrWhiteSpace(query)) return true;

        var blob = string.Join(" ", m.Time, m.Title, m.Type, m.Briefing,
            string.Join(" ", matchingRoles.Select(r => $"{r.Side} {r.Unit} {r.Aircraft} {r.Role} {r.Description}")));
        return blob.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private bool HasActiveMissionFilters()
        => !string.IsNullOrWhiteSpace(SearchBox.Text)
           || (SideFilterCombo.SelectedItem as string ?? "All sides") != "All sides"
           || (TypeFilterCombo.SelectedItem as string ?? "All types") != "All types"
           || (MapFilterCombo.SelectedItem as string ?? "All maps") != "All maps"
           || (GroupFilterCombo.SelectedItem as string ?? "All units") != "All units"
           || (AircraftFilterCombo.SelectedItem as string ?? "All aircraft") != "All aircraft";

    private void ShowMission(CampaignMission? mission)
    {
        if (mission is null)
        {
            ClearDetails();
            return;
        }

        _selectedMission = mission;
        ApplyMapForMission(mission);
        MissionTitleText.Text = mission.Title;
        MissionMetaText.Text = mission.IsLocked
            ? $"{mission.DisplayTime} / locked until earlier campaign missions are completed"
            : $"{mission.DisplayTime} / {mission.Type} / {mission.RouteSummary}";
        UpdateDisplayedBriefing();
        UpdateHeaderWidgets();

        if (mission.IsLocked && !_unlockCampaignsForTesting)
        {
            RoleList.ItemsSource = null;
            RouteList.ItemsSource = null;
            _selectedRole = null;
            _displayRoutes = [];
            BriefingSummaryText.Text = "This sortie is locked by the vanilla campaign progress state.";
            FullBriefingText.Text = BriefingSummaryText.Text;
            MapHintText.Text = "Complete earlier campaign sorties to unlock this mission.";
            DrawRoutes();
            LoadGallery(mission);
            LoadPilotLogEntries();
            return;
        }

        var side = SideFilterCombo.SelectedItem as string ?? "All sides";
        var group = GroupFilterCombo.SelectedItem as string ?? "All units";
        var aircraft = AircraftFilterCombo.SelectedItem as string ?? "All aircraft";
        var displayRoles = mission.Playable.Where(r => RoleMatches(r, side, group, aircraft)).ToList();
        if (displayRoles.Count == 0 && side == "All sides" && group == "All units" && aircraft == "All aircraft") displayRoles = mission.Playable;

        RoleList.ItemsSource = displayRoles;
        RoleList.SelectedIndex = displayRoles.Count > 0 ? 0 : -1;
        UpdateDisplayedBriefing();
        if (displayRoles.Count == 0)
        {
            _selectedRole = null;
            UpdateDisplayedBriefing();
            RefreshSelectedFlightRoute();
        }

        LoadGallery(mission);
        LoadPilotLogEntries();
    }

    private void ClearDetails()
    {
        _selectedMission = null;
        _displayRoutes = [];
        MissionTitleText.Text = "Select a mission";
        MissionMetaText.Text = "Campaign detail panel";
        BriefingSummaryText.Text = "";
        FullBriefingText.Text = "";
        RouteList.ItemsSource = null;
        MapHintText.Text = "";
        UpdateHeaderWidgets();
        RoleList.ItemsSource = null;
        _selectedRole = null;
        _galleryItems.Clear();
        ShowGalleryItem();
        RouteCanvas.Children.Clear();
    }

    private void RefreshSelectedFlightRoute()
    {
        if (_selectedMission is null)
        {
            _displayRoutes = [];
            RouteList.ItemsSource = null;
            MapHintText.Text = "";
            DrawRoutes();
            return;
        }

        var routes = _selectedRole is null
            ? new List<CampaignRoute>()
            : RoutesForRole(_selectedMission, _selectedRole);

        _displayRoutes = routes;
        RouteList.ItemsSource = routes;
        UpdateDisplayedBriefing();
        MapHintText.Text = _selectedRole is null
            ? "Choose an available flight to show its route."
            : $"{_selectedRole.Unit} route shown / {routes.Count} route{(routes.Count == 1 ? "" : "s")} matched.";
        FitMapToRoute(routes);
    }

    private void UpdateDisplayedBriefing()
    {
        if (_selectedMission is null)
        {
            BriefingSummaryText.Text = "";
            FullBriefingText.Text = "";
            return;
        }

        var briefing = FirstNonEmpty(_selectedRole?.Description, _selectedMission.Briefing);
        BriefingSummaryText.Text = briefing;
        FullBriefingText.Text = briefing;
    }

    private void UpdateHeaderWidgets()
    {
        var sideFilter = SideFilterCombo.SelectedItem as string ?? "All sides";
        var side = sideFilter == "All sides"
            ? FirstNonEmpty(_selectedRole?.Side, _selectedMission?.PrimarySide, "All sides")
            : sideFilter;

        SideInsigniaImage.Source = side switch
        {
            "RAF" => MakeImage("/Assets/Roundel.png"),
            "Luftwaffe" => MakeImage("/Assets/IronCross.png"),
            "Regia Aeronautica" => MakeImage("/Assets/RegiaAeronautica.png"),
            _ => null
        };

        var weatherSource = FirstNonEmpty(_selectedMission?.Weather, _selectedDay?.Weather, _selectedRole?.Description, _selectedMission?.Briefing, _selectedDay?.Summary);
        var weather = ClassifyWeather(weatherSource);
        WeatherIconText.Text = weather.Icon;
        MetReportWidget.ToolTip = MakeThemedTooltip("MET REPORT", weather.Title, weather.Detail);
    }

    private static (string Icon, string Title, string Detail) ClassifyWeather(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return ("WX", "No report", "Weather report unavailable.");

        var lower = text.ToLowerInvariant();
        if (lower.Contains("thunder")) return ("TS", "Thunderstorms", ShortHeaderText(text, 92));
        if (lower.Contains("rain") || lower.Contains("showers")) return ("☂", "Rain showers", ShortHeaderText(text, 92));
        if (lower.Contains("overcast")) return ("☁", "Overcast", ShortHeaderText(text, 92));
        if (lower.Contains("partly") || lower.Contains("cumulus") || lower.Contains("cloud")) return ("◐", "Partly cloudy", ShortHeaderText(text, 92));
        if (lower.Contains("clear") || lower.Contains("sun") || lower.Contains("visibility is excellent")) return ("☀", "Clear", ShortHeaderText(text, 92));
        return ("WX", "Weather report", ShortHeaderText(text, 92));
    }

    private static string ShortHeaderText(string text, int max = 210)
    {
        var clean = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
        if (clean.Length == 0) return "No summary available.";

        var sentenceEnd = clean.IndexOf('.');
        if (sentenceEnd > 35 && sentenceEnd < max) clean = clean[..(sentenceEnd + 1)];
        return clean.Length <= max ? clean : clean[..Math.Max(0, max - 1)] + "…";
    }

    private static List<CampaignRoute> RoutesForRole(CampaignMission mission, CampaignRole role)
    {
        var routes = mission.Routes
            .Where(r => !string.IsNullOrWhiteSpace(role.AirGroupId) &&
                        r.AirGroupId.Equals(role.AirGroupId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (routes.Count > 0) return routes;

        routes = mission.Routes
            .Where(r => r.Side.Equals(role.Side, StringComparison.OrdinalIgnoreCase)
                        && AircraftMatches(r.Aircraft, role.Aircraft)
                        && (GroupMatches(FirstNonEmpty(r.Unit, r.Briefing, r.Id), FirstNonEmpty(role.Unit, role.Id))
                            || NormalToken(r.Briefing).Contains(NormalToken(role.Unit))
                            || NormalToken(role.Unit).Contains(NormalToken(r.Briefing))))
            .ToList();
        if (routes.Count > 0) return routes;

        return mission.Routes
            .Where(r => r.Side.Equals(role.Side, StringComparison.OrdinalIgnoreCase)
                        && AircraftMatches(r.Aircraft, role.Aircraft))
            .Take(1)
            .ToList();
    }

    private void LoadGallery(CampaignMission mission)
    {
        _galleryItems.Clear();
        _galleryItems.AddRange(mission.Slides
            .Where(s => !string.IsNullOrWhiteSpace(s.Src))
            .Select(s => new GalleryItem(s.Src, FirstNonEmpty(s.Title, s.SectionName, "Briefing image"), s.Caption)));

        _galleryItems.AddRange(mission.Images
            .Where(i => _galleryItems.All(g => !g.Image.Equals(i, StringComparison.OrdinalIgnoreCase)))
            .Select(i => new GalleryItem(i, System.IO.Path.GetFileNameWithoutExtension(i), "")));

        _galleryIndex = 0;
        ShowGalleryItem();
    }

    private void GalleryPrev_Click(object sender, RoutedEventArgs e)
    {
        if (_galleryItems.Count == 0) return;
        _galleryIndex = (_galleryIndex - 1 + _galleryItems.Count) % _galleryItems.Count;
        ShowGalleryItem();
    }

    private void GalleryNext_Click(object sender, RoutedEventArgs e)
    {
        if (_galleryItems.Count == 0) return;
        _galleryIndex = (_galleryIndex + 1) % _galleryItems.Count;
        ShowGalleryItem();
    }

    private void ShowGalleryItem()
    {
        var hasItems = _galleryItems.Count > 0;
        GalleryEmptyText.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
        GalleryImage.Source = hasItems ? MakeImage(_galleryItems[_galleryIndex].Image) : null;
        GalleryTitleText.Text = hasItems ? _galleryItems[_galleryIndex].Title : "";
        GalleryCaptionText.Text = hasItems ? _galleryItems[_galleryIndex].Caption : "";
        GalleryCounterText.Text = hasItems ? $"{_galleryIndex + 1} / {_galleryItems.Count}" : "0 / 0";
    }

    private void DrawRoutes(bool updateLayout = true)
    {
        RouteCanvas.Children.Clear();
        if (updateLayout) UpdateMapLayout();
        if (_selectedMission is null || _selectedCampaign is null) return;

        DrawIntelMarkers(_selectedMission.IntelLocations);

        var routes = _displayRoutes.Where(r => r.Waypoints.Count >= 2).ToList();
        if (routes.Count == 0)
        {
            _lastRoutePoints = [];
            return;
        }

        var mapWidth = Math.Max(1, _mapNativeWidth);
        var mapHeight = Math.Max(1, _mapNativeHeight);
        var currentMap = CurrentMapInfo();
        var projected = routes.ToDictionary(r => r, r => r.Waypoints.Select(w => ProjectWaypoint(w, currentMap)).ToList());
        var validProjected = projected.SelectMany(p => p.Value).Count(p => IsOnMap(p, mapWidth, mapHeight));
        var totalProjected = projected.Sum(p => p.Value.Count);
        if (totalProjected == 0 || validProjected < totalProjected)
            projected = BuildFittedRoutePoints(routes, mapWidth, mapHeight);

        foreach (var route in routes)
        {
            var points = projected[route].Select(MapPointToViewport).ToList();
            AddRoutePolyline(route, points);
        }

        _lastRoutePoints = projected.SelectMany(p => p.Value).ToList();
    }

    private void DrawIntelMarkers(IEnumerable<CampaignIntelLocation> locations)
    {
        var pixels = s_mapCalibrationPixels.Value;
        foreach (var location in locations)
        {
            var point = FindCalibrationPixel(pixels, location.Name)
                        ?? FindCalibrationPixel(pixels, $"{location.Name} centre")
                        ?? FindCalibrationPixel(pixels, $"{location.Name} town")
                        ?? FindIntelAliasPixel(pixels, location.Name);
            if (point is null) continue;

            var viewport = MapPointToViewport(point.Value);
            var radius = location.Kind.Equals("Target", StringComparison.OrdinalIgnoreCase) ? 34 : 26;
            var brush = location.Kind.Equals("Target", StringComparison.OrdinalIgnoreCase)
                ? new SolidColorBrush(Color.FromArgb(70, 214, 162, 67))
                : new SolidColorBrush(Color.FromArgb(62, 92, 176, 212));
            var stroke = location.Kind.Equals("Target", StringComparison.OrdinalIgnoreCase)
                ? new SolidColorBrush(Color.FromRgb(214, 162, 67))
                : new SolidColorBrush(Color.FromRgb(92, 176, 212));

            var circle = new Ellipse
            {
                Width = radius * 2,
                Height = radius * 2,
                Fill = brush,
                Stroke = stroke,
                StrokeThickness = 2,
                IsHitTestVisible = true,
                ToolTip = MakeThemedTooltip(location.Kind.ToUpperInvariant(), location.Name, location.Context)
            };
            ToolTipService.SetInitialShowDelay(circle, 0);
            ToolTipService.SetBetweenShowDelay(circle, 0);
            Canvas.SetLeft(circle, viewport.X - radius);
            Canvas.SetTop(circle, viewport.Y - radius);
            RouteCanvas.Children.Add(circle);

            var label = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(150, 6, 8, 9)),
                BorderBrush = stroke,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(5, 2, 5, 2),
                Child = new TextBlock
                {
                    Text = location.Name,
                    Foreground = Brushes.White,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold
                },
                IsHitTestVisible = true,
                ToolTip = MakeThemedTooltip(location.Kind.ToUpperInvariant(), location.Name, location.Context)
            };
            ToolTipService.SetInitialShowDelay(label, 0);
            ToolTipService.SetBetweenShowDelay(label, 0);
            Canvas.SetLeft(label, viewport.X + radius + 4);
            Canvas.SetTop(label, viewport.Y - 12);
            RouteCanvas.Children.Add(label);
        }
    }

    private static ToolTip MakeThemedTooltip(string heading, string title, string detail)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = heading,
            Foreground = new SolidColorBrush(Color.FromRgb(0xC7, 0xA0, 0x66)),
            FontFamily = new FontFamily("Georgia"),
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 5)
        });
        panel.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 4)
        });
        panel.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(detail) ? "No further report available." : detail,
            Foreground = new SolidColorBrush(Color.FromRgb(0xC5, 0xB9, 0xA8)),
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 310
        });

        return new ToolTip { Content = panel };
    }

    private static Point? FindIntelAliasPixel(IReadOnlyDictionary<string, Point> pixels, string name)
        => NormalizeCalibrationName(name) switch
        {
            "pasdecalais" => FindCalibrationPixel(pixels, "Calais-Marck"),
            "calais" => FindCalibrationPixel(pixels, "Calais-Marck"),
            "southend" => FindCalibrationPixel(pixels, "Southend town"),
            "boulogne" => FindCalibrationPixel(pixels, "Boulogne town"),
            "folkestone" => FindCalibrationPixel(pixels, "Folkestone town"),
            "london" => FindCalibrationPixel(pixels, "London centre"),
            "tobruk" => FindCalibrationPixel(pixels, "Tobruk town"),
            "derna" => FindCalibrationPixel(pixels, "Derna town"),
            "gazala" => FindCalibrationPixel(pixels, "Gazala no1"),
            "eladem" => FindCalibrationPixel(pixels, "el Adem No1"),
            "sollum" => FindCalibrationPixel(pixels, "Sollum"),
            "halfaya" => FindCalibrationPixel(pixels, "Halfaya"),
            "sidibarrani" => FindCalibrationPixel(pixels, "Sidi Barrani town"),
            "sidirezegh" => FindCalibrationPixel(pixels, "Sidi Rezegh"),
            "biralhakim" => FindCalibrationPixel(pixels, "Bi'r al Hakim"),
            "gambut" => FindCalibrationPixel(pixels, "Gambut"),
            _ => null
        };

    private Dictionary<CampaignRoute, List<Point>> BuildFittedRoutePoints(List<CampaignRoute> routes, double mapWidth, double mapHeight)
    {
        var allPoints = routes.SelectMany(r => r.Waypoints).ToList();
        var minX = allPoints.Min(p => p.X);
        var maxX = allPoints.Max(p => p.X);
        var minY = allPoints.Min(p => p.Y);
        var maxY = allPoints.Max(p => p.Y);
        var spanX = Math.Max(1, maxX - minX);
        var spanY = Math.Max(1, maxY - minY);
        var pad = 80.0;
        var scale = Math.Min((mapWidth - pad * 2) / spanX, (mapHeight - pad * 2) / spanY);
        if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0) return [];

        var drawW = spanX * scale;
        var drawH = spanY * scale;
        var offsetX = (mapWidth - drawW) / 2;
        var offsetY = (mapHeight - drawH) / 2;

        return routes.ToDictionary(
            route => route,
            route => route.Waypoints
                .Select(w => new Point(offsetX + (w.X - minX) * scale, offsetY + (w.Y - minY) * scale))
                .ToList());
    }

    private void AddRoutePolyline(CampaignRoute route, List<Point> points)
    {
        var halo = new Polyline
        {
            Points = new PointCollection(points),
            Stroke = Brushes.Black,
            StrokeThickness = 9,
            Opacity = 0.72,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            IsHitTestVisible = false
        };
        RouteCanvas.Children.Add(halo);

        var line = new Polyline
        {
            Points = new PointCollection(points),
            Stroke = SideBrush(route.Side),
            StrokeThickness = 3,
            StrokeDashArray = [4, 3],
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            IsHitTestVisible = false
        };
        RouteCanvas.Children.Add(line);

        foreach (var point in points)
        {
            var dot = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = SideBrush(route.Side),
                Stroke = Brushes.Black,
                StrokeThickness = 2,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(dot, point.X - 4);
            Canvas.SetTop(dot, point.Y - 4);
            RouteCanvas.Children.Add(dot);
        }
    }

    private static Point ProjectWaypoint(CampaignWaypoint waypoint, CampaignMapInfo map)
    {
        if (waypoint.X >= 0 && waypoint.X <= map.Width && waypoint.Y >= 0 && waypoint.Y <= map.Height)
            return new Point(waypoint.X, waypoint.Y);

        if (map.Image.Contains("tobruk", StringComparison.OrdinalIgnoreCase))
        {
            return ProjectTobrukWaypoint(waypoint);
        }

        return ProjectDoverWaypoint(waypoint);
    }

    private static Point ProjectTobrukWaypoint(CampaignWaypoint waypoint)
    {
        // Affine calibration from Gazala No.1, Tobruk harbour, and Derna return-base samples.
        return new Point(
            0.02784972319537199 * waypoint.X + 0.0011232029442724161 * waypoint.Y - 538.5104479757409,
            -0.0006868356464329456 * waypoint.X - 0.029988795759057475 * waypoint.Y + 10795.582265094994);
    }

    private static Point ProjectDoverWaypoint(CampaignWaypoint waypoint)
    {
        var pixels = s_mapCalibrationPixels.Value;
        var hawkingePixel = FindCalibrationPixel(pixels, "Hawkinge") ?? new Point(2454, 1065);
        var calaisPixel = FindCalibrationPixel(pixels, "Calais-Marck") ?? new Point(3015, 1234);

        var hawkingeGame = new Point(234931.02, 232454.51);
        var pihenGame = new Point(277141.95, 206459.20);
        var xScale = (calaisPixel.X - hawkingePixel.X) / (pihenGame.X - hawkingeGame.X);
        var yScale = (calaisPixel.Y - hawkingePixel.Y) / (pihenGame.Y - hawkingeGame.Y);

        return new Point(
            hawkingePixel.X + (waypoint.X - hawkingeGame.X) * xScale,
            hawkingePixel.Y + (waypoint.Y - hawkingeGame.Y) * yScale);
    }

    private static Point? FindCalibrationPixel(IReadOnlyDictionary<string, Point> pixels, string name)
        => pixels.TryGetValue(NormalizeCalibrationName(name), out var point) ? point : null;

    private static IReadOnlyDictionary<string, Point> LoadMapCalibrationPixels()
    {
        var result = new Dictionary<string, Point>(StringComparer.OrdinalIgnoreCase);
        var path = System.IO.Path.Combine(AppContext.BaseDirectory, "map_calibration_data.txt");
        if (!File.Exists(path))
            path = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "map_calibration_data.txt");
        if (!File.Exists(path)) return result;

        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || !line.Contains(':')) continue;

            var split = line.IndexOf(':');
            var match = System.Text.RegularExpressions.Regex.Match(line[(split + 1)..], @"(-?\d+(?:\.\d+)?)\s*[,.;]\s*(-?\d+(?:\.\d+)?)");
            if (!match.Success) continue;
            if (!double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x)) continue;
            if (!double.TryParse(match.Groups[2].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y)) continue;
            AddCalibrationNameVariants(result, line[..split], new Point(x, y));
        }

        return result;
    }

    private static void AddCalibrationNameVariants(Dictionary<string, Point> result, string rawName, Point point)
    {
        foreach (var part in rawName.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var clean = part.Trim();
            if (clean.Length == 0) continue;
            result[NormalizeCalibrationName(clean)] = point;

            var withoutParenthetical = System.Text.RegularExpressions.Regex.Replace(clean, @"\s*\([^)]*\)", "").Trim();
            if (!string.IsNullOrWhiteSpace(withoutParenthetical))
                result[NormalizeCalibrationName(withoutParenthetical)] = point;

            var withoutLgCode = System.Text.RegularExpressions.Regex.Replace(withoutParenthetical, @"\bLG\s*\d+\b", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
            if (!string.IsNullOrWhiteSpace(withoutLgCode))
                result[NormalizeCalibrationName(withoutLgCode)] = point;
        }
    }

    private static string NormalizeCalibrationName(string value)
        => System.Text.RegularExpressions.Regex.Replace(value.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "");

    private static bool IsOnMap(Point point, double width, double height)
        => point.X >= -40 && point.Y >= -40 && point.X <= width + 40 && point.Y <= height + 40;

    private CampaignMapInfo CurrentMapInfo()
        => _selectedMission?.Map ?? _selectedCampaign?.Map ?? new CampaignMapInfo();

    private void ApplyMapForMission(CampaignMission? mission)
    {
        var map = mission?.Map ?? _selectedCampaign?.Map ?? new CampaignMapInfo();
        var changed = !_currentMapImage.Equals(map.Image, StringComparison.OrdinalIgnoreCase);
        if (changed)
        {
            _currentMapImage = map.Image;
            MapImage.Source = MakeImage(map.Image);
            ConfigureMapStage();
            return;
        }

        _mapNativeWidth = map.Width;
        _mapNativeHeight = map.Height;
        if (MapImage.Source is BitmapSource bitmap && bitmap.PixelWidth > 0 && bitmap.PixelHeight > 0)
        {
            _mapNativeWidth = bitmap.PixelWidth;
            _mapNativeHeight = bitmap.PixelHeight;
        }

        UpdateMapLayout();
    }

    private void ConfigureMapStage()
    {
        var map = CurrentMapInfo();
        _mapNativeWidth = map.Width;
        _mapNativeHeight = map.Height;
        if (MapImage.Source is BitmapSource bitmap && bitmap.PixelWidth > 0 && bitmap.PixelHeight > 0)
        {
            _mapNativeWidth = bitmap.PixelWidth;
            _mapNativeHeight = bitmap.PixelHeight;
        }

        FitMapToViewport();
    }

    private void MapViewport_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateMapLayout();
        DrawRoutes();
    }

    private void MapFit_Click(object sender, RoutedEventArgs e)
    {
        if (_displayRoutes.Count > 0) FitMapToRoute(_displayRoutes);
        else FitMapToViewport();
    }

    private void MapZoomIn_Click(object sender, RoutedEventArgs e)
        => ZoomMap(1.2, new Point(MapViewport.ActualWidth / 2, MapViewport.ActualHeight / 2));

    private void MapZoomOut_Click(object sender, RoutedEventArgs e)
        => ZoomMap(1 / 1.2, new Point(MapViewport.ActualWidth / 2, MapViewport.ActualHeight / 2));

    private void MapViewport_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        ZoomMap(e.Delta > 0 ? 1.45 : 1 / 1.45, e.GetPosition(MapViewport));
        e.Handled = true;
    }

    private void MapViewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _mapDragging = true;
        _mapDragStart = e.GetPosition(MapViewport);
        _mapTranslateStart = _mapPan;
        MapViewport.CaptureMouse();
    }

    private void MapViewport_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_mapDragging) return;
        var position = e.GetPosition(MapViewport);
        _mapPan = new Point(_mapTranslateStart.X + position.X - _mapDragStart.X, _mapTranslateStart.Y + position.Y - _mapDragStart.Y);
        UpdateMapLayout();
        DrawRoutes(false);
    }

    private void MapViewport_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _mapDragging = false;
        MapViewport.ReleaseMouseCapture();
    }

    private void FitMapToViewport()
    {
        _mapZoom = 1;
        _mapPan = new Point(0, 0);
        UpdateMapLayout();
        DrawRoutes();
    }

    private void FitMapToRoute(List<CampaignRoute> routes)
    {
        if (_selectedCampaign is null || routes.Count == 0 || MapViewport.ActualWidth <= 1 || MapViewport.ActualHeight <= 1)
        {
            FitMapToViewport();
            return;
        }

        var points = ProjectRoutePoints(routes);
        if (points.Count < 2)
        {
            FitMapToViewport();
            return;
        }

        FitMapToPoints(points, 96, true);
    }

    private List<Point> ProjectRoutePoints(List<CampaignRoute> routes)
    {
        if (_selectedCampaign is null) return [];

        var mapWidth = Math.Max(1, _mapNativeWidth);
        var mapHeight = Math.Max(1, _mapNativeHeight);
        var currentMap = CurrentMapInfo();
        var projected = routes.SelectMany(r => r.Waypoints.Select(w => ProjectWaypoint(w, currentMap))).ToList();
        var validProjected = projected.Count(p => IsOnMap(p, mapWidth, mapHeight));
        if (projected.Count == 0 || validProjected < projected.Count)
            projected = BuildFittedRoutePoints(routes, mapWidth, mapHeight).SelectMany(kvp => kvp.Value).ToList();

        return projected;
    }

    private void FitMapToPoints(List<Point> points, double padding, bool animate)
    {
        var minX = points.Min(p => p.X);
        var maxX = points.Max(p => p.X);
        var minY = points.Min(p => p.Y);
        var maxY = points.Max(p => p.Y);
        var routeWidth = Math.Max(1, maxX - minX);
        var routeHeight = Math.Max(1, maxY - minY);
        var baseFit = Math.Min(MapViewport.ActualWidth / _mapNativeWidth, MapViewport.ActualHeight / _mapNativeHeight);
        if (double.IsNaN(baseFit) || double.IsInfinity(baseFit) || baseFit <= 0) baseFit = 1;

        var availableW = Math.Max(80, MapViewport.ActualWidth - padding * 2);
        var availableH = Math.Max(80, MapViewport.ActualHeight - padding * 2);
        var targetZoom = Math.Clamp(Math.Min(availableW / (routeWidth * baseFit), availableH / (routeHeight * baseFit)), 1.15, 5.6);
        var mapWidth = _mapNativeWidth * baseFit * targetZoom;
        var mapHeight = _mapNativeHeight * baseFit * targetZoom;
        var centerX = (minX + maxX) / 2;
        var centerY = (minY + maxY) / 2;
        var targetPan = new Point(
            MapViewport.ActualWidth / 2 - (MapViewport.ActualWidth - mapWidth) / 2 - centerX / _mapNativeWidth * mapWidth,
            MapViewport.ActualHeight / 2 - (MapViewport.ActualHeight - mapHeight) / 2 - centerY / _mapNativeHeight * mapHeight);

        if (animate) AnimateMapTo(targetZoom, targetPan, 280);
        else
        {
            _mapZoom = targetZoom;
            _mapPan = targetPan;
            UpdateMapLayout();
            DrawRoutes();
        }
    }

    private void ZoomMap(double factor, Point origin)
    {
        var oldRect = GetMapRect();
        var mapX = (origin.X - oldRect.X) / Math.Max(1, oldRect.Width);
        var mapY = (origin.Y - oldRect.Y) / Math.Max(1, oldRect.Height);
        var targetZoom = Math.Clamp(_mapZoom * factor, 1.0, 8.0);
        var fit = Math.Min(MapViewport.ActualWidth / _mapNativeWidth, MapViewport.ActualHeight / _mapNativeHeight);
        if (double.IsNaN(fit) || double.IsInfinity(fit) || fit <= 0) fit = 1;
        var width = _mapNativeWidth * fit * targetZoom;
        var height = _mapNativeHeight * fit * targetZoom;
        var targetPan = new Point(
            origin.X - (MapViewport.ActualWidth - width) / 2 - mapX * width,
            origin.Y - (MapViewport.ActualHeight - height) / 2 - mapY * height);
        AnimateMapTo(targetZoom, targetPan, 150);
    }

    private void AnimateMapTo(double targetZoom, Point targetPan, double durationMs)
    {
        _mapAnimationTimer?.Stop();
        _mapAnimationTimer = null;

        var startZoom = _mapZoom;
        var startPan = _mapPan;
        var started = DateTime.UtcNow;
        var timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
        _mapAnimationTimer = timer;
        timer.Tick += (_, _) =>
        {
            var progress = Math.Min(1, (DateTime.UtcNow - started).TotalMilliseconds / durationMs);
            var eased = progress < 0.5
                ? 4 * progress * progress * progress
                : 1 - Math.Pow(-2 * progress + 2, 3) / 2;
            _mapZoom = startZoom + (targetZoom - startZoom) * eased;
            _mapPan = new Point(
                startPan.X + (targetPan.X - startPan.X) * eased,
                startPan.Y + (targetPan.Y - startPan.Y) * eased);
            UpdateMapLayout();
            DrawRoutes(false);
            if (progress < 1) return;
            _mapZoom = targetZoom;
            _mapPan = targetPan;
            UpdateMapLayout();
            DrawRoutes(false);
            timer.Stop();
            if (ReferenceEquals(_mapAnimationTimer, timer)) _mapAnimationTimer = null;
        };
        timer.Start();
    }

    private void UpdateMapLayout()
    {
        if (MapViewport.ActualWidth <= 1 || MapViewport.ActualHeight <= 1 || _mapNativeWidth <= 1 || _mapNativeHeight <= 1) return;

        var rect = GetMapRect();
        Canvas.SetLeft(MapImage, rect.X);
        Canvas.SetTop(MapImage, rect.Y);
        MapImage.Width = rect.Width;
        MapImage.Height = rect.Height;
        RouteCanvas.Width = MapViewport.ActualWidth;
        RouteCanvas.Height = MapViewport.ActualHeight;
    }

    private Rect GetMapRect()
    {
        var fit = Math.Min(MapViewport.ActualWidth / _mapNativeWidth, MapViewport.ActualHeight / _mapNativeHeight);
        if (double.IsNaN(fit) || double.IsInfinity(fit) || fit <= 0) fit = 1;
        var width = _mapNativeWidth * fit * _mapZoom;
        var height = _mapNativeHeight * fit * _mapZoom;
        return new Rect(
            (MapViewport.ActualWidth - width) / 2 + _mapPan.X,
            (MapViewport.ActualHeight - height) / 2 + _mapPan.Y,
            width,
            height);
    }

    private Point MapPointToViewport(Point point)
    {
        var rect = GetMapRect();
        return new Point(rect.X + point.X / _mapNativeWidth * rect.Width, rect.Y + point.Y / _mapNativeHeight * rect.Height);
    }

    private void AutofillPilotLog_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedMission is null) return;
        var role = CurrentLogRole();
        PilotResultCombo.SelectedItem = "Completed";
        PilotLandingCombo.SelectedItem = "Returned to base";
        PilotAirClaimsBox.Text = "0";
        PilotGroundClaimsBox.Text = "0";
        PilotDamageBox.Text = "None recorded";
        PilotNotesBox.Text = $"{role.Unit} sortie in {_selectedMission.Title}.";
        PilotLogStatusText.Text = "Draft filled from selected flight.";
    }

    private void SavePilotLog_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedCampaign is null || _selectedDay is null || _selectedMission is null)
        {
            PilotLogStatusText.Text = "Select a mission first.";
            return;
        }

        var role = CurrentLogRole();
        PilotLogService.Save(new PilotLogEntry
        {
            Campaign = _selectedCampaign.Title,
            Date = _selectedDay.Date,
            MissionTime = _selectedMission.DisplayTime,
            MissionTitle = _selectedMission.Title,
            Unit = role.Unit,
            Side = role.Side,
            Aircraft = role.Aircraft,
            Result = PilotResultCombo.SelectedItem as string ?? "",
            Landing = PilotLandingCombo.SelectedItem as string ?? "",
            AirClaims = ParseInt(PilotAirClaimsBox.Text),
            GroundClaims = ParseInt(PilotGroundClaimsBox.Text),
            Damage = PilotDamageBox.Text.Trim(),
            Notes = PilotNotesBox.Text.Trim()
        });

        PilotLogStatusText.Text = "Saved locally.";
        LoadPilotLogEntries();
    }

    private CampaignRole CurrentLogRole()
    {
        if (_selectedMission is null) return new CampaignRole { Unit = "Flight Section" };
        if (_selectedRole is not null) return _selectedRole;

        var side = SideFilterCombo.SelectedItem as string ?? "All sides";
        var group = GroupFilterCombo.SelectedItem as string ?? "All units";
        var aircraft = AircraftFilterCombo.SelectedItem as string ?? "All aircraft";
        return _selectedMission.Playable.FirstOrDefault(r => RoleMatches(r, side, group, aircraft))
               ?? _selectedMission.Playable.FirstOrDefault()
               ?? new CampaignRole { Unit = "Flight Section", Aircraft = "Aircraft TBC", Side = "Unknown" };
    }

    private void LoadPilotLogEntries()
    {
        var selectedTitle = _selectedMission?.Title ?? "";
        PilotLogList.ItemsSource = PilotLogService.Load()
            .Where(e => string.IsNullOrWhiteSpace(selectedTitle) || e.MissionTitle.Equals(selectedTitle, StringComparison.OrdinalIgnoreCase))
            .Take(8)
            .Select(e => $"{e.Display}\n{e.Summary}")
            .ToList();
    }

    private static int ParseInt(string value)
        => int.TryParse(value, out var parsed) ? parsed : 0;

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) return match;
            var descendant = FindVisualChild<T>(child);
            if (descendant is not null) return descendant;
        }

        return null;
    }

    private static System.Windows.Media.Brush SideBrush(string side)
        => side switch
        {
            "RAF" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x38, 0xbd, 0xf8)),
            "Luftwaffe" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xf9, 0x73, 0x16)),
            "Regia Aeronautica" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x22, 0xc5, 0x5e)),
            _ => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xfb, 0xbf, 0x24))
        };

    private static bool RoleMatches(CampaignRole role, string side, string group, string aircraft)
        => SideMatches(role.Side, side)
           && GroupMatches(FirstNonEmpty(role.Unit, role.Id), group)
           && AircraftMatches(role.Aircraft, aircraft);

    private static bool RouteMatches(CampaignRoute route, string side, string group, string aircraft)
        => SideMatches(route.Side, side)
           && GroupMatches(FirstNonEmpty(route.Unit, route.Briefing, route.Id), group)
           && AircraftMatches(route.Aircraft, aircraft);

    private static bool SideMatches(string value, string filter)
        => filter == "All sides" || value.Equals(filter, StringComparison.OrdinalIgnoreCase);

    private static bool MapMatches(CampaignMission mission, string filter)
        => filter == "All maps"
           || mission.Map.Theater.Equals(filter, StringComparison.OrdinalIgnoreCase);

    private static bool GroupMatches(string value, string filter)
        => filter == "All units" || value.Equals(filter, StringComparison.OrdinalIgnoreCase);

    private static bool AircraftMatches(string value, string filter)
        => filter == "All aircraft" || value.Equals(filter, StringComparison.OrdinalIgnoreCase);

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "";

    private static string NormalToken(string value)
        => System.Text.RegularExpressions.Regex.Replace(value.ToLowerInvariant(), @"[^a-z0-9]+", "");

    private static BitmapImage? MakeImage(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            var uri = path.StartsWith("/", StringComparison.Ordinal)
                ? new Uri(path, UriKind.Relative)
                : new Uri(path, UriKind.Absolute);
            return new BitmapImage(uri);
        }
        catch
        {
            return null;
        }
    }
}
