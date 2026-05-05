using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using PlaneLoadoutWpfTest.Screens;
using PlaneLoadoutWpfTest.Services;

namespace PlaneLoadoutWpfTest;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private const double DesignW = 1920, DesignH = 1080;

    private FrameworkElement? _currentScreen;
    public FrameworkElement? CurrentScreen
    {
        get => _currentScreen;
        private set { _currentScreen = value; OnPropertyChanged(); }
    }

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        NavigationService.ScreenChanged += OnScreenChanged;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Apply any saved window resolution override (0,0 = maximised = default).
        var saved = AppSettingsService.Current;
        if (saved.WindowWidth > 0 && saved.WindowHeight > 0)
        {
            WindowState = WindowState.Normal;
            Width  = saved.WindowWidth;
            Height = saved.WindowHeight;
            Left   = (SystemParameters.WorkArea.Width  - saved.WindowWidth)  / 2;
            Top    = (SystemParameters.WorkArea.Height - saved.WindowHeight) / 2;
        }

        ApplyResolutionScaling(DesignW, DesignH);
        Activate();
        NavigationService.GoTo(new MainMenuScreen());
    }

    private void OnScreenChanged(FrameworkElement newScreen)
    {
        var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new QuadraticEase()
        };
        fadeOut.Completed += (_, _) =>
        {
            CurrentScreen = newScreen;
            var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(150))
            {
                EasingFunction = new QuadraticEase()
            };
            ScreenHost.BeginAnimation(OpacityProperty, fadeIn);
        };
        ScreenHost.BeginAnimation(OpacityProperty, fadeOut);
    }

    private void ApplyResolutionScaling(double targetW, double targetH)
    {
        if (targetW >= DesignW && targetH >= DesignH)
        {
            RootGrid.LayoutTransform = Transform.Identity;
        }
        else
        {
            double scale = Math.Min(targetW / DesignW, targetH / DesignH);
            RootGrid.LayoutTransform = new ScaleTransform(scale, scale);
        }
    }

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        var p  = e.GetPosition(this);
        var nx = p.X / Math.Max(1, ActualWidth)  - 0.5;   // –0.5 … +0.5
        var ny = p.Y / Math.Max(1, ActualHeight) - 0.5;

        // ── Parallax: large coefficient gap creates clear depth separation ──
        // Blueprint is the "near" layer  — moves more
        BlueprintParallax.X  = nx * -28.0;
        BlueprintParallax.Y  = ny * -16.0;
        // Background photo is the "far" layer — moves much less
        BackgroundParallax.X = nx *  -5.0;
        BackgroundParallax.Y = ny *  -4.0;

        // ── Point-light tracks the cursor ────────────────────────────────────
        LightTransform.X = nx *  48.0;
        LightTransform.Y = ny *  36.0;

        // ── Directional sheen: rotate gradient angle with mouse ───────────────
        // Full side-to-side sweep ≈ ±40°; slight vertical component adds depth.
        LightGradientAngle.Angle = nx * 55.0 + ny * 20.0;
    }

    // ── Gear / settings overlay ───────────────────────────────────────────────

    private void Gear_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsOverlay.Visibility == System.Windows.Visibility.Visible)
            SettingsOverlay.Close();
        else
            SettingsOverlay.Open();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
