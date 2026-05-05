using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using PlaneLoadoutWpfTest.Screens;
using PlaneLoadoutWpfTest.Services;

namespace PlaneLoadoutWpfTest;

public partial class SettingsOverlay : UserControl
{
    private const double PanelWidth = 480.0;
    private const int    SlideDurMs = 220;

    public SettingsOverlay()
    {
        InitializeComponent();
        LoadPaths();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Open()
    {
        LoadPaths();
        Visibility = Visibility.Visible;
        AnimatePanel(PanelWidth, 0.0);
    }

    public void Close()
        => AnimatePanel(0.0, PanelWidth, () => Visibility = Visibility.Collapsed);

    // ── Event handlers ────────────────────────────────────────────────────────

    private void Backdrop_MouseDown(object sender,
        System.Windows.Input.MouseButtonEventArgs e) => Close();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void BrowseGameRoot_Click(object sender, RoutedEventArgs e)
        => BrowseFolder("Select Cliffs of Dover Install Folder", GameRootBox);

    private void BrowseDocsRoot_Click(object sender, RoutedEventArgs e)
        => BrowseFolder("Select Documents Folder", DocsRootBox);

    private void ExportCampaignJson_Click(object sender, RoutedEventArgs e)
    {
        if (NavigationService.Current is CampaignScreen screen)
        {
            screen.ExportParsedCampaignJson();
            StatusLabel.Text = "Campaign JSON export opened.";
            StatusLabel.Foreground = (Brush)FindResource("TextGoldBrush");
            return;
        }

        if (CampaignBoardSession.HasCampaigns)
        {
            var path = CampaignBoardSession.ExportJson();
            StatusLabel.Text = string.IsNullOrWhiteSpace(path) ? "Campaign JSON export cancelled." : "Campaign JSON exported.";
            StatusLabel.Foreground = (Brush)FindResource("TextGoldBrush");
            return;
        }

        StatusLabel.Text = "Open or scan the Campaign Browser first, then export.";
        StatusLabel.Foreground = (Brush)FindResource("DangerBrush");
    }

    private void ExportCampaignReport_Click(object sender, RoutedEventArgs e)
    {
        if (NavigationService.Current is CampaignScreen screen)
        {
            screen.ExportParserDiagnostics();
            StatusLabel.Text = "Parser report export opened.";
            StatusLabel.Foreground = (Brush)FindResource("TextGoldBrush");
            return;
        }

        if (CampaignBoardSession.HasCampaigns)
        {
            var path = CampaignBoardSession.ExportDiagnostics();
            StatusLabel.Text = string.IsNullOrWhiteSpace(path) ? "Parser report export cancelled." : "Parser report exported.";
            StatusLabel.Foreground = (Brush)FindResource("TextGoldBrush");
            return;
        }

        StatusLabel.Text = "Open or scan the Campaign Browser first, then create a report.";
        StatusLabel.Foreground = (Brush)FindResource("DangerBrush");
    }

    private void ExportCampaignDiscovery_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            FileName = "campaign-discovery-diagnostics.md",
            Filter = "Markdown files (*.md)|*.md",
            AddExtension = true,
            OverwritePrompt = true
        };
        if (dialog.ShowDialog() != true) return;

        File.WriteAllText(dialog.FileName, CampaignSelectionService.BuildDiscoveryDiagnostics());
        StatusLabel.Text = "Campaign discovery report exported.";
        StatusLabel.Foreground = (Brush)FindResource("TextGoldBrush");
    }

    // ── Scan ──────────────────────────────────────────────────────────────────

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        var settings = BuildSettings();
        AppSettingsService.Save(settings);

        ScanButton.IsEnabled   = false;
        StatusLabel.Text       = "Scanning…";
        StatusLabel.Foreground = (Brush)FindResource("TextDimBrush");

        var progress = new Progress<string>(msg => StatusLabel.Text = msg);

        try
        {
            await MissionCatalog.PopulateAsync(settings, Dispatcher, progress);
            CampaignSelectionService.ClearCache();

            int total = MissionCatalog.All.Count;
            StatusLabel.Text = total == 0
                ? "No missions found. Check folder paths above."
                : $"✓  Found {total} mission{(total == 1 ? "" : "s")} across all folders.";

            StatusLabel.Foreground = (Brush)FindResource(
                total == 0 ? "DangerBrush" : "TextGoldBrush");
        }
        catch (Exception ex)
        {
            StatusLabel.Text       = $"✗  Scan failed: {ex.Message}";
            StatusLabel.Foreground = (Brush)FindResource("DangerBrush");
        }
        finally
        {
            ScanButton.IsEnabled = true;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void LoadPaths()
    {
        var s = AppSettingsService.Current;
        GameRootBox.Text = s.GameRootPath;
        DocsRootBox.Text = s.DocumentsRootPath;

        // Select the matching resolution entry
        SelectResolutionCombo(s.WindowWidth, s.WindowHeight);
        SelectLogoGlowCombo(s.LogoGlowMode);
    }

    private AppSettings BuildSettings()
    {
        var (w, h) = ParseResolutionTag();
        return new AppSettings
        {
            GameRootPath             = GameRootBox.Text.Trim(),
            DocumentsRootPath        = DocsRootBox.Text.Trim(),
            WindowWidth              = w,
            WindowHeight             = h,
            LogoGlowMode             = ParseLogoGlowMode()
        };
    }

    // ── Logo glow helpers ────────────────────────────────────────────────────

    private void SelectLogoGlowCombo(string mode)
    {
        foreach (ComboBoxItem item in LogoGlowCombo.Items)
        {
            if (item.Tag is string tag
                && tag.Equals(mode, StringComparison.OrdinalIgnoreCase))
            {
                LogoGlowCombo.SelectedItem = item;
                return;
            }
        }
        LogoGlowCombo.SelectedIndex = 0;
    }

    private string ParseLogoGlowMode()
        => LogoGlowCombo.SelectedItem is ComboBoxItem { Tag: string tag }
            ? tag
            : "Fire";

    private void LogoGlowCombo_Changed(object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        AppSettingsService.Save(BuildSettings());
    }

    // ── Resolution helpers ────────────────────────────────────────────────────

    private void SelectResolutionCombo(int w, int h)
    {
        string tag = $"{w},{h}";
        foreach (ComboBoxItem item in ResolutionCombo.Items)
        {
            if (item.Tag is string t && t == tag)
            {
                ResolutionCombo.SelectedItem = item;
                return;
            }
        }
        ResolutionCombo.SelectedIndex = 0; // default = maximised
    }

    private (int w, int h) ParseResolutionTag()
    {
        if (ResolutionCombo.SelectedItem is ComboBoxItem { Tag: string tag })
        {
            var parts = tag.Split(',');
            if (parts.Length == 2
                && int.TryParse(parts[0], out int w)
                && int.TryParse(parts[1], out int h))
                return (w, h);
        }
        return (0, 0);
    }

    private void ResolutionCombo_Changed(object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        // Apply immediately to the owning window so the user sees the effect.
        if (!IsLoaded) return;
        var win = Window.GetWindow(this);
        if (win is null) return;

        var (w, h) = ParseResolutionTag();
        if (w == 0)
        {
            win.WindowState = WindowState.Maximized;
        }
        else
        {
            win.WindowState = WindowState.Normal;
            win.Width  = w;
            win.Height = h;
            win.Left   = (SystemParameters.WorkArea.Width  - w) / 2;
            win.Top    = (SystemParameters.WorkArea.Height - h) / 2;
        }
    }

    private static void BrowseFolder(string title, TextBox target)
    {
        var dialog = new OpenFolderDialog { Title = title };
        if (Directory.Exists(target.Text))
            dialog.InitialDirectory = target.Text;
        if (dialog.ShowDialog() == true)
            target.Text = dialog.FolderName;
    }

    // ── Slide animation ───────────────────────────────────────────────────────

    private void AnimatePanel(double from, double to, Action? onComplete = null)
    {
        var anim = new DoubleAnimation
        {
            From           = from,
            To             = to,
            Duration       = TimeSpan.FromMilliseconds(SlideDurMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        if (onComplete is not null)
            anim.Completed += (_, _) => onComplete();

        PanelSlide.BeginAnimation(TranslateTransform.XProperty, anim);
    }
}
