using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using PlaneLoadoutWpfTest.Services;

namespace PlaneLoadoutWpfTest.Screens;

public partial class MainMenuScreen : UserControl
{
    public MainMenuScreen()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyLogoGlowMode(AppSettingsService.Current.LogoGlowMode);
        Unloaded += (_, _) => AppSettingsService.SettingsSaved -= OnSettingsSaved;
        AppSettingsService.SettingsSaved += OnSettingsSaved;
    }

    private void OnSettingsSaved(AppSettings settings)
        => ApplyLogoGlowMode(settings.LogoGlowMode);

    private void UserControl_MouseMove(object sender, MouseEventArgs e)
    {
        var p = e.GetPosition(this);
        var nx = p.X / Math.Max(1, ActualWidth) - 0.5;
        var ny = p.Y / Math.Max(1, ActualHeight) - 0.5;

        MenuBackgroundParallax.X = nx * -8.0;
        MenuBackgroundParallax.Y = ny * -4.0;
        MenuLightTransform.X = nx * 46.0;
        MenuLightTransform.Y = ny * 34.0;
    }

    private void ApplyLogoGlowMode(string mode)
    {
        LogoGlow.BeginAnimation(OpacityProperty, null);

        if (mode.Equals("Breathe", StringComparison.OrdinalIgnoreCase))
        {
            LogoGlow.BeginAnimation(OpacityProperty, new DoubleAnimation
            {
                From = 0.26,
                To = 0.62,
                Duration = TimeSpan.FromSeconds(2.8),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            });
            return;
        }

        var fire = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromSeconds(2.7),
            RepeatBehavior = RepeatBehavior.Forever
        };
        fire.KeyFrames.Add(new EasingDoubleKeyFrame(0.30, KeyTime.FromPercent(0.00)));
        fire.KeyFrames.Add(new EasingDoubleKeyFrame(0.46, KeyTime.FromPercent(0.09)));
        fire.KeyFrames.Add(new EasingDoubleKeyFrame(0.34, KeyTime.FromPercent(0.22)));
        fire.KeyFrames.Add(new EasingDoubleKeyFrame(0.67, KeyTime.FromPercent(0.31)));
        fire.KeyFrames.Add(new EasingDoubleKeyFrame(0.39, KeyTime.FromPercent(0.47)));
        fire.KeyFrames.Add(new EasingDoubleKeyFrame(0.55, KeyTime.FromPercent(0.58)));
        fire.KeyFrames.Add(new EasingDoubleKeyFrame(0.36, KeyTime.FromPercent(0.73)));
        fire.KeyFrames.Add(new EasingDoubleKeyFrame(0.71, KeyTime.FromPercent(0.86)));
        fire.KeyFrames.Add(new EasingDoubleKeyFrame(0.32, KeyTime.FromPercent(1.00)));
        LogoGlow.BeginAnimation(OpacityProperty, fire);
    }

    private void SinglePlayer_Click(object sender, RoutedEventArgs e)
        => NavigationService.GoTo(new SinglePlayerMenuScreen());

    private void Multiplayer_Click(object sender, RoutedEventArgs e)
        => NavigationService.GoTo(new MultiplayerMenuScreen());

    private void Options_Click(object sender, RoutedEventArgs e)
        => NavigationService.GoTo(new OptionsMenuScreen());

    private void Extras_Click(object sender, RoutedEventArgs e)
        => NavigationService.GoTo(new ExtrasMenuScreen());

    private void Exit_Click(object sender, RoutedEventArgs e)
        => Application.Current.Shutdown();
}
