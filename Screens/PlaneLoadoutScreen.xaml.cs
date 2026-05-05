using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using PlaneLoadoutWpfTest;
using PlaneLoadoutWpfTest.Services;

namespace PlaneLoadoutWpfTest.Screens;

public partial class PlaneLoadoutScreen : UserControl, INotifyPropertyChanged
{
    private const double DesignW = 1920, DesignH = 1080;

    private static readonly string[] DefaultBelt =
    [
        "Incendiary, B, .303 inch",
        "Nitrocellulose, Mark VIz, De Wilde",
        "Armour Piercing, W, .303 inch",
        "Nitrocellulose, Mark Iz"
    ];

    private int _horizontalConvergence = 500;
    private int _verticalConvergence   = 300;
    private double _fuelPercent        = 100;
    private int _selectedResolutionIndex = 2;
    private bool _defaultConvergence   = false;

    public bool DefaultConvergence
    {
        get => _defaultConvergence;
        set { _defaultConvergence = value; OnPropertyChanged(); }
    }

    public ObservableCollection<string> MainBeltItems { get; } = new(DefaultBelt);

    public ObservableCollection<BulletOption> BulletOptions { get; } = new()
    {
        new("Ball, .303 inch, Magazine Rifle, Cordite, Mark I",               "Assets/bullet_ball.png"),
        new("Ball, .303 inch, Cordite, Mark VI",                              "Assets/bullet_ball.png"),
        new("Ball, .303 inch, Cordite, Mark VII",                             "Assets/bullet_ball.png"),
        new("Tracer (Yellow), G, 500 yd, .303 inch, Cordite, Mark I",        "Assets/bullet_tracer_yellow.png"),
        new("Tracer (Yellow), G, Infantry Long Range, 1000 yd, .303 inch, Cordite, Mark II", "Assets/bullet_tracer_yellow.png"),
        new("Tracer (Red), G, Naval, 800 yd, .303 inch, Cordite, Mark III",  "Assets/bullet_tracer_red.png"),
        new("Tracer (Burgundy), G, Aircraft Night Dimmed, 550 yd, .303 inch, Cordite, Mark V", "Assets/bullet_tracer_burgundy.png"),
        new("Armour Piercing, W, .303 inch, Nitrocellulose, Mark Iz",        "Assets/bullet_ap.png")
    };

    public int HorizontalConvergence
    {
        get => _horizontalConvergence;
        set { _horizontalConvergence = Math.Clamp(value, 50, 800); OnPropertyChanged(); }
    }

    public int VerticalConvergence
    {
        get => _verticalConvergence;
        set { _verticalConvergence = Math.Clamp(value, 50, 800); OnPropertyChanged(); }
    }

    public double FuelPercent
    {
        get => _fuelPercent;
        set
        {
            _fuelPercent = Math.Clamp(value, 0, 100);
            OnPropertyChanged();
            OnPropertyChanged(nameof(FuelWeight));
            OnPropertyChanged(nameof(CurrentWeight));
        }
    }

    public int FuelWeight    => (int)Math.Round(324 * FuelPercent / 100.0);
    public int CurrentWeight => 2331 + 90 + 176 + FuelWeight;
    public int TakeoffWeight => 2331 + 90 + 176 + 324;
    public string RoundelImagePath { get; private set; } = "/Assets/Roundel.png";

    public int SelectedResolutionIndex
    {
        get => _selectedResolutionIndex;
        set { _selectedResolutionIndex = value; OnPropertyChanged(); }
    }

    public PlaneLoadoutScreen()
    {
        InitializeComponent();
        DataContext = this;
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyResolutionScaling(DesignW, DesignH);
        UpdateVisiblePanel(animate: false);
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

    private void ResolutionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || SelectedResolutionIndex < 0) return;

        (double w, double h) = SelectedResolutionIndex switch
        {
            0 => (1366,  768),
            1 => (1600,  900),
            2 => (1920, 1080),
            3 => (2560, 1440),
            4 => (1280,  720),
            _ => (1920, 1080)
        };

        ApplyResolutionScaling(w, h);
    }

    private void LoadoutTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || e.Source != LoadoutTabs) return;
        UpdateVisiblePanel(animate: true);
    }

    private void UpdateVisiblePanel(bool animate)
    {
        FadePanel(WeaponSetsContent, LoadoutTabs.SelectedIndex == 0, animate);
        FadePanel(GunsContent,       LoadoutTabs.SelectedIndex == 1, animate);
    }

    private static void FadePanel(UIElement panel, bool show, bool animate)
    {
        if (!animate)
        {
            panel.Opacity    = show ? 1 : 0;
            panel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            return;
        }

        panel.Visibility = Visibility.Visible;
        var anim = new DoubleAnimation
        {
            To             = show ? 1.0 : 0.0,
            Duration       = TimeSpan.FromMilliseconds(180),
            EasingFunction = new QuadraticEase()
        };
        if (!show)
            anim.Completed += (_, _) => panel.Visibility = Visibility.Collapsed;
        else
            panel.Opacity = 0;
        panel.BeginAnimation(OpacityProperty, anim, HandoffBehavior.SnapshotAndReplace);
    }

    private void ConvergenceStep_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag }) return;
        DefaultConvergence = false;
        switch (tag)
        {
            case "H-": HorizontalConvergence -= 25; break;
            case "H+": HorizontalConvergence += 25; break;
            case "V-": VerticalConvergence   -= 25; break;
            case "V+": VerticalConvergence   += 25; break;
        }
    }

    private void BeltInsert_Click(object sender, RoutedEventArgs e)
    {
        if (BulletsListBox.SelectedItem is not BulletOption bullet) return;
        var idx = MainBeltListBox.SelectedIndex;
        if (idx < 0)
            MainBeltItems.Add(bullet.Name);
        else
            MainBeltItems.Insert(idx, bullet.Name);
        MainBeltListBox.SelectedIndex = idx < 0 ? MainBeltItems.Count - 1 : idx;
    }

    private void BeltAdd_Click(object sender, RoutedEventArgs e)
    {
        if (BulletsListBox.SelectedItem is not BulletOption bullet) return;
        MainBeltItems.Add(bullet.Name);
        MainBeltListBox.SelectedIndex = MainBeltItems.Count - 1;
    }

    private void BeltRemove_Click(object sender, RoutedEventArgs e)
    {
        var idx = MainBeltListBox.SelectedIndex;
        if (idx < 0) return;
        MainBeltItems.RemoveAt(idx);
        if (MainBeltItems.Count > 0)
            MainBeltListBox.SelectedIndex = Math.Min(idx, MainBeltItems.Count - 1);
    }

    private void BeltDefault_Click(object sender, RoutedEventArgs e)
    {
        MainBeltItems.Clear();
        foreach (var item in DefaultBelt)
            MainBeltItems.Add(item);
    }

    private void Back_Click(object sender, RoutedEventArgs e)
        => NavigationService.Back();

    private void Ok_Click(object sender, RoutedEventArgs e)
        => NavigationService.Back();

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
