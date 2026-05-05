using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using PlaneLoadoutWpfTest.Services;

namespace PlaneLoadoutWpfTest.Screens;

public partial class QuickMissionScreen : UserControl
{
    private readonly List<ScannedMission> _quickMissions = [];

    public QuickMissionScreen()
    {
        InitializeComponent();

        // Refresh the mission list whenever the settings overlay triggers a scan.
        MissionCatalog.CatalogUpdated += OnCatalogUpdated;
    }

    // ── Loaded ────────────────────────────────────────────────────────────────

    private async void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        PopulateWeatherPresets();
        PopulateAircraftList();

        if (!MissionCatalog.HasPopulated)
        {
            EmptyMissionHint.Text = "Scanning configured Quick Mission folders...";
            EmptyMissionHint.Visibility = Visibility.Visible;
            await MissionCatalog.PopulateAsync(AppSettingsService.Current, Dispatcher);
            return;
        }

        LoadMissionsFromCatalog(MissionCatalog.QuickMissions);
    }

    private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        => MissionCatalog.CatalogUpdated -= OnCatalogUpdated;

    private void OnCatalogUpdated()
    {
        // Already on the UI thread (MissionCatalog dispatches this).
        LoadMissionsFromCatalog(MissionCatalog.QuickMissions);
    }

    // ── Time pickers (HH : MM) — DrumSpinners ────────────────────────────────

    /// <summary>
    /// Returns the currently selected mission time as a CloD-compatible integer
    /// (e.g. 09:30 → 930, 14:00 → 1400).
    /// </summary>
    public int GetHourMin() => HourSpinner.Value * 100 + MinuteSpinner.Value;

    // ── Weather presets ───────────────────────────────────────────────────────

    private void PopulateWeatherPresets()
    {
        if (WeatherCombo.Items.Count > 0) return;

        foreach (var preset in DataService.WeatherPresets)
        {
            WeatherCombo.Items.Add(new ComboBoxItem
            {
                Content = preset.Name,
                Tag     = preset          // full record stored for logic below
            });
        }
        WeatherCombo.SelectedIndex = 0;
    }

    private void WeatherCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (WeatherCombo.SelectedItem is not ComboBoxItem { Tag: DataService.WeatherPreset preset })
            return;

        bool hasClouds = preset.CloudBaseKft.HasValue;
        CloudBasePanel.Visibility = hasClouds ? Visibility.Visible : Visibility.Collapsed;

        // Pre-fill the cloud base spinner with the preset's suggested altitude
        if (hasClouds)
            CloudBaseSpinner.Value = preset.CloudBaseKft!.Value;
    }

    // ── Aircraft list ─────────────────────────────────────────────────────────

    private void PopulateAircraftList()
    {
        RefreshAircraftList();
    }

    private void RefreshAircraftList()
    {
        AircraftListBox.Items.Clear();

        var side = (SideFilterCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Allies";
        var role = (RoleFilterCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All";

        var aircraft = DataService.GetAircraftBySide(side);

        if (role != "All" && Enum.TryParse<DataService.AircraftRole>(role, out var roleEnum))
            aircraft = aircraft.Where(a => a.Role == roleEnum);

        foreach (var ac in aircraft)
            AircraftListBox.Items.Add(new ListBoxItem { Content = ac.Name, Tag = ac });

        if (AircraftListBox.Items.Count > 0)
            AircraftListBox.SelectedIndex = 0;
    }

    private void SideFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded) RefreshAircraftList();
    }

    private void RoleFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded) RefreshAircraftList();
    }

    private void Aircraft_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

    // ── Mission list ──────────────────────────────────────────────────────────

    private void MissionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MissionList.SelectedItem is not ListBoxItem item) return;

        // If the item has a MissionScannerService.MissionEntry attached (from browse),
        // populate detail labels from it; otherwise fall back to friendly defaults.
        if (item.Tag is ScannedMission entry)
        {
            MissionTitleLabel.Text  = entry.Title;
            MissionDescLabel.Text   = string.IsNullOrWhiteSpace(entry.Description)
                                        ? "No briefing text available."
                                        : entry.Description;
            MissionMapLabel.Text    = entry.Map;
            MissionTimeLabel.Text   = entry.TimeHHMM;
            MissionMetaGrid.Visibility = Visibility.Visible;

            // Sync hour/minute spinners to mission time
            if (entry.TimeHHMM.Length == 5
                && int.TryParse(entry.TimeHHMM[..2], out int hh)
                && int.TryParse(entry.TimeHHMM[3..], out int mm))
            {
                HourSpinner.Value   = hh;
                MinuteSpinner.Value = (mm / 5) * 5;   // round to nearest 5-min step
            }

            // Mission image
            if (entry.ImagePaths.Count > 0)
            {
                try
                {
                    DetailImage.Source     = new BitmapImage(new Uri(entry.ImagePaths[0]));
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
        }
        else
        {
            MissionTitleLabel.Text  = item.Content?.ToString() ?? "";
            MissionDescLabel.Text   = GetBuiltInDescription(MissionTitleLabel.Text);
            MissionMapLabel.Text    = item.Tag?.ToString() ?? "—";
            MissionTimeLabel.Text   = "—";
            MissionMetaGrid.Visibility = Visibility.Collapsed;
            DetailImage.Source     = null;
            NoImageHint.Visibility = Visibility.Visible;
        }
    }

    private void MapFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ApplyMissionFilter();
    }

    private static string GetBuiltInDescription(string title) => title switch
    {
        "Channel Sweep"  => "A routine sweep over the English Channel. Engage any enemy aircraft encountered and return to base.",
        "Bomber Escort"  => "Escort a formation of bombers to their target and provide top cover on the return leg.",
        "Interception"   => "Scramble to intercept an inbound enemy raid before it reaches its target.",
        "Ground Attack"  => "Attack designated enemy ground targets. Watch for light flak.",
        "Scramble!"      => "Immediate scramble order. Get airborne as fast as possible.",
        "Free Hunt"      => "Free-ranging fighter sweep. Seek and destroy enemy aircraft in the assigned sector.",
        "Desert Patrol"  => "Patrol the front line in the Western Desert. Report any movement along the coastal road.",
        "Convoy Strike"  => "Attack an enemy supply convoy. Expect fighter escort.",
        _                => "No briefing available."
    };

    // ── Mission folder browser ────────────────────────────────────────────────

    private void BrowseMissions_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select IL-2 Cliffs of Dover Missions Folder"
        };

        if (dialog.ShowDialog() != true) return;

        var missions = MissionScannerService.ScanFolder(
                           dialog.FolderName, MissionCategory.Quick).ToList();

        if (missions.Count == 0)
        {
            MessageBox.Show(
                "No .mis files found in the selected folder.",
                "Browse Missions", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        LoadMissionsFromCatalog(missions);
    }

    // ── Mission list population ───────────────────────────────────────────────

    private void LoadMissionsFromCatalog(IEnumerable<ScannedMission> missions)
    {
        _quickMissions.Clear();
        _quickMissions.AddRange(missions.OrderBy(m => m.Title, StringComparer.OrdinalIgnoreCase));
        ApplyMissionFilter();
    }

    private void ApplyMissionFilter()
    {
        var missions = _quickMissions.Where(MatchesSelectedMap).ToList();
        MissionList.Items.Clear();
        foreach (var m in missions)
        {
            MissionList.Items.Add(new ListBoxItem
            {
                Content = m.Title,
                ToolTip = $"{m.Map}  ·  {m.TimeHHMM}",
                Tag     = m
            });
        }

        EmptyMissionHint.Text = _quickMissions.Count == 0
            ? "No quick missions found. Check the Cliffs of Dover install folder or Documents root in Settings."
            : "No quick missions match this theatre filter.";
        EmptyMissionHint.Visibility = MissionList.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        if (MissionList.Items.Count > 0)
            MissionList.SelectedIndex = 0;
        else
            ClearMissionDetail();
    }

    private bool MatchesSelectedMap(ScannedMission mission)
    {
        var selected = (MapFilterCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All theatres";
        if (selected.StartsWith("All", StringComparison.OrdinalIgnoreCase)) return true;

        var map = mission.Map.ToLowerInvariant();
        return selected.StartsWith("English", StringComparison.OrdinalIgnoreCase)
            ? map.Contains("english") || map.Contains("channel")
            : map.Contains("tobruk") || map.Contains("desert") || map.Contains("libya");
    }

    private void ClearMissionDetail()
    {
        MissionTitleLabel.Text = "No mission selected";
        MissionDescLabel.Text = "";
        MissionMapLabel.Text = "";
        MissionTimeLabel.Text = "";
        MissionMetaGrid.Visibility = Visibility.Collapsed;
        DetailImage.Source = null;
        NoImageHint.Visibility = Visibility.Visible;
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    private void Back_Click(object sender, RoutedEventArgs e)
        => NavigationService.Back();

    private void Realism_Click(object sender, RoutedEventArgs e)
        => NavigationService.GoTo(new RealismScreen());
}
