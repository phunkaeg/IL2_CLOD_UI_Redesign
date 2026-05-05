using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using PlaneLoadoutWpfTest.Services;

namespace PlaneLoadoutWpfTest.Screens;

public partial class SingleMissionScreen : UserControl
{
    // Current theatre filter: "all" | "bob" | "tobruk"
    private string _theatre = "all";
    private ScannedMission? _selectedMission;
    private double _mapNativeWidth = 3993;
    private double _mapNativeHeight = 3507;

    public SingleMissionScreen()
    {
        InitializeComponent();
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private async void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        MissionCatalog.CatalogUpdated += OnCatalogUpdated;
        if (!MissionCatalog.HasPopulated)
        {
            EmptyHint.Text = "Scanning configured Single Mission folders...";
            EmptyHint.Visibility = Visibility.Visible;
            await MissionCatalog.PopulateAsync(AppSettingsService.Current, Dispatcher);
            return;
        }

        LoadMissionsFromCatalog();
    }

    private void UserControl_Unloaded(object sender, RoutedEventArgs e)
    {
        MissionCatalog.CatalogUpdated -= OnCatalogUpdated;
    }

    private void OnCatalogUpdated() => LoadMissionsFromCatalog();

    // ── List population ───────────────────────────────────────────────────────

    private void LoadMissionsFromCatalog()
    {
        IReadOnlyList<ScannedMission> source = _theatre switch
        {
            "bob"    => MissionCatalog.BoBSingleMissions,
            "tobruk" => MissionCatalog.TobrukSingleMissions,
            _        => MissionCatalog.AllSingleMissions,
        };

        MissionList.ItemsSource = source;
        EmptyHint.Text = MissionCatalog.HasPopulated
            ? "No single missions found. Check the Cliffs of Dover install folder or Documents root in Settings."
            : "Scanning configured Single Mission folders...";
        EmptyHint.Visibility = source.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // Re-select the first item so the detail pane isn't blank
        if (source.Count > 0)
            MissionList.SelectedIndex = 0;
        else
            ClearDetail();
    }

    // ── Theatre tab filter ────────────────────────────────────────────────────

    private void Tab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            _theatre = tag;
            LoadMissionsFromCatalog();
        }
    }

    // ── Detail pane ───────────────────────────────────────────────────────────

    private void MissionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MissionList.SelectedItem is not ScannedMission m)
        {
            ClearDetail();
            return;
        }

        _selectedMission = m;
        DetailTitle.Text       = m.Title;
        DetailMap.Text         = m.Map;
        DetailTime.Text        = m.TimeHHMM;
        DetailTheatre.Text     = m.Category switch
        {
            MissionCategory.BoBSingle    => "Battle of Britain",
            MissionCategory.TobrukSingle => "Tobruk / Desert War",
            _                            => m.Category.ToString()
        };
        DetailCategory.Text    = m.Category.ToString();
        DetailCampaign.Text    = string.IsNullOrWhiteSpace(m.CampaignName)
                                     ? "—"
                                     : m.CampaignName;
        DetailDescription.Text = string.IsNullOrWhiteSpace(m.Description)
            ? "No briefing text available for this mission."
            : FormatBriefingForDisplay(m.Description);
        DetailMissionFile.Text = m.FilePath;
        DetailBriefingFile.Text = string.IsNullOrWhiteSpace(m.BriefingFilePath)
            ? "—"
            : m.BriefingFilePath;

        // Mission image — use first discovered image if available
        if (m.ImagePaths.Count > 0)
        {
            try
            {
                DetailImage.Source = new BitmapImage(new Uri(m.ImagePaths[0]));
                NoImageHint.Visibility = Visibility.Collapsed;
            }
            catch
            {
                DetailImage.Source     = null;
                NoImageHint.Visibility = Visibility.Visible;
            }
        }
        else
        {
            DetailImage.Source     = null;
            NoImageHint.Visibility = Visibility.Visible;
        }

        LoadMissionMap(m);
    }

    private void ClearDetail()
    {
        _selectedMission = null;
        DetailTitle.Text       = string.Empty;
        DetailMap.Text         = string.Empty;
        DetailTime.Text        = string.Empty;
        DetailTheatre.Text     = string.Empty;
        DetailCategory.Text    = string.Empty;
        DetailCampaign.Text    = string.Empty;
        DetailDescription.Text = string.Empty;
        DetailMissionFile.Text = string.Empty;
        DetailBriefingFile.Text = string.Empty;
        DetailImage.Source     = null;
        NoImageHint.Visibility = Visibility.Visible;
        MissionMapImage.Source = null;
        MissionRouteCanvas.Children.Clear();
        MissionMapHint.Text = "";
    }

    private void MissionMapViewport_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateMissionMapLayout();
        DrawMissionRoutes();
    }

    private void LoadMissionMap(ScannedMission mission)
    {
        var isTobruk = IsTobrukMap(mission.Map);
        _mapNativeWidth = isTobruk ? 10000 : 3993;
        _mapNativeHeight = isTobruk ? 9917 : 3507;
        MissionMapImage.Source = MakeImage(isTobruk ? "/Assets/tobruk_map.jpg" : "/Assets/strait_of_dover_map.jpg");
        if (MissionMapImage.Source is BitmapSource bitmap && bitmap.PixelWidth > 0 && bitmap.PixelHeight > 0)
        {
            _mapNativeWidth = bitmap.PixelWidth;
            _mapNativeHeight = bitmap.PixelHeight;
        }

        UpdateMissionMapLayout();
        DrawMissionRoutes();
    }

    private void UpdateMissionMapLayout()
    {
        if (MissionMapViewport.ActualWidth <= 1 || MissionMapViewport.ActualHeight <= 1 || _mapNativeWidth <= 1 || _mapNativeHeight <= 1) return;

        var fit = Math.Min(MissionMapViewport.ActualWidth / _mapNativeWidth, MissionMapViewport.ActualHeight / _mapNativeHeight);
        var width = _mapNativeWidth * fit;
        var height = _mapNativeHeight * fit;
        Canvas.SetLeft(MissionMapImage, (MissionMapViewport.ActualWidth - width) / 2);
        Canvas.SetTop(MissionMapImage, (MissionMapViewport.ActualHeight - height) / 2);
        MissionMapImage.Width = width;
        MissionMapImage.Height = height;
        MissionRouteCanvas.Width = MissionMapViewport.ActualWidth;
        MissionRouteCanvas.Height = MissionMapViewport.ActualHeight;
    }

    private void DrawMissionRoutes()
    {
        MissionRouteCanvas.Children.Clear();
        if (_selectedMission is null || MissionMapViewport.ActualWidth <= 1 || MissionMapViewport.ActualHeight <= 1) return;

        var routes = _selectedMission.Routes.Where(r => r.Waypoints.Count >= 2).ToList();
        if (routes.Count == 0)
        {
            MissionMapHint.Text = "No route waypoints detected in this mission.";
            return;
        }

        var projected = routes.ToDictionary(r => r, r => r.Waypoints.Select(w => ProjectWaypoint(w, _selectedMission.Map)).ToList());
        var total = projected.Sum(kvp => kvp.Value.Count);
        var valid = projected.SelectMany(kvp => kvp.Value).Count(p => IsOnMap(p, _mapNativeWidth, _mapNativeHeight));
        if (total == 0 || valid < total)
            projected = BuildFittedRoutePoints(routes);

        foreach (var route in routes)
        {
            var points = projected[route].Select(MapPointToViewport).ToList();
            AddRoutePolyline(route, points);
        }

        MissionMapHint.Text = $"{routes.Count} route{(routes.Count == 1 ? "" : "s")} plotted from mission waypoints.";
    }

    private Dictionary<ScannedRoute, List<Point>> BuildFittedRoutePoints(List<ScannedRoute> routes)
    {
        var allPoints = routes.SelectMany(r => r.Waypoints).ToList();
        var minX = allPoints.Min(p => p.X);
        var maxX = allPoints.Max(p => p.X);
        var minY = allPoints.Min(p => p.Y);
        var maxY = allPoints.Max(p => p.Y);
        var spanX = Math.Max(1, maxX - minX);
        var spanY = Math.Max(1, maxY - minY);
        var pad = 80.0;
        var scale = Math.Min((_mapNativeWidth - pad * 2) / spanX, (_mapNativeHeight - pad * 2) / spanY);
        if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0) return [];

        var drawW = spanX * scale;
        var drawH = spanY * scale;
        var offsetX = (_mapNativeWidth - drawW) / 2;
        var offsetY = (_mapNativeHeight - drawH) / 2;

        return routes.ToDictionary(
            route => route,
            route => route.Waypoints
                .Select(w => new Point(offsetX + (w.X - minX) * scale, offsetY + (w.Y - minY) * scale))
                .ToList());
    }

    private Point MapPointToViewport(Point point)
    {
        var x = Canvas.GetLeft(MissionMapImage);
        var y = Canvas.GetTop(MissionMapImage);
        return new Point(
            x + point.X / _mapNativeWidth * MissionMapImage.Width,
            y + point.Y / _mapNativeHeight * MissionMapImage.Height);
    }

    private void AddRoutePolyline(ScannedRoute route, List<Point> points)
    {
        var halo = new Polyline
        {
            Points = new PointCollection(points),
            Stroke = Brushes.Black,
            StrokeThickness = 7,
            Opacity = 0.72,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            IsHitTestVisible = false
        };
        MissionRouteCanvas.Children.Add(halo);

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
        MissionRouteCanvas.Children.Add(line);

        foreach (var point in points)
        {
            var dot = new Ellipse
            {
                Width = 7,
                Height = 7,
                Fill = SideBrush(route.Side),
                Stroke = Brushes.Black,
                StrokeThickness = 1.5,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(dot, point.X - 3.5);
            Canvas.SetTop(dot, point.Y - 3.5);
            MissionRouteCanvas.Children.Add(dot);
        }
    }

    private static Point ProjectWaypoint(ScannedWaypoint waypoint, string map)
    {
        var isTobruk = IsTobrukMap(map);
        var width = isTobruk ? 10000 : 3993;
        var height = isTobruk ? 9917 : 3507;
        if (waypoint.X >= 0 && waypoint.X <= width && waypoint.Y >= 0 && waypoint.Y <= height)
            return new Point(waypoint.X, waypoint.Y);

        return isTobruk ? ProjectTobrukWaypoint(waypoint) : ProjectDoverWaypoint(waypoint);
    }

    private static Point ProjectTobrukWaypoint(ScannedWaypoint waypoint)
        => new(
            0.02784972319537199 * waypoint.X + 0.0011232029442724161 * waypoint.Y - 538.5104479757409,
            -0.0006868356464329456 * waypoint.X - 0.029988795759057475 * waypoint.Y + 10795.582265094994);

    private static Point ProjectDoverWaypoint(ScannedWaypoint waypoint)
    {
        var hawkingePixel = new Point(2454, 1065);
        var calaisPixel = new Point(3015, 1234);
        var hawkingeGame = new Point(234931.02, 232454.51);
        var pihenGame = new Point(277141.95, 206459.20);
        var xScale = (calaisPixel.X - hawkingePixel.X) / (pihenGame.X - hawkingeGame.X);
        var yScale = (calaisPixel.Y - hawkingePixel.Y) / (pihenGame.Y - hawkingeGame.Y);

        return new Point(
            hawkingePixel.X + (waypoint.X - hawkingeGame.X) * xScale,
            hawkingePixel.Y + (waypoint.Y - hawkingeGame.Y) * yScale);
    }

    private static bool IsOnMap(Point point, double width, double height)
        => point.X >= -40 && point.Y >= -40 && point.X <= width + 40 && point.Y <= height + 40;

    private static Brush SideBrush(string side)
        => side switch
        {
            "RAF" => new SolidColorBrush(Color.FromRgb(0x38, 0xbd, 0xf8)),
            "Luftwaffe" => new SolidColorBrush(Color.FromRgb(0xf9, 0x73, 0x16)),
            "Regia Aeronautica" => new SolidColorBrush(Color.FromRgb(0x22, 0xc5, 0x5e)),
            _ => new SolidColorBrush(Color.FromRgb(0xfb, 0xbf, 0x24))
        };

    private static string FormatBriefingForDisplay(string value)
    {
        var text = System.Text.RegularExpressions.Regex.Replace(value, @"\[(info|mainintro|intro|objective|objectives?|briefing|weather|success|failure|fail|debrief)\]", "\n\n$1\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\[(\d+)\]", "\n\n[$1] ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"(?:\r?\n\s*){3,}", "\n\n");
        return text.Trim();
    }

    private static bool IsTobrukMap(string value)
        => value.Contains("tobruk", StringComparison.OrdinalIgnoreCase)
           || value.Contains("desert", StringComparison.OrdinalIgnoreCase)
           || value.Contains("libya", StringComparison.OrdinalIgnoreCase);

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

    // ── Navigation ────────────────────────────────────────────────────────────

    private void Back_Click(object sender, RoutedEventArgs e)
        => NavigationService.Back();
}
