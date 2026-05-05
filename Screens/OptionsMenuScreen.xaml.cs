using System.Windows;
using System.Windows.Controls;
using PlaneLoadoutWpfTest.Services;

namespace PlaneLoadoutWpfTest.Screens;

public partial class OptionsMenuScreen : UserControl
{
    public OptionsMenuScreen()
    {
        InitializeComponent();
    }

    private void Pilot_Click(object sender, RoutedEventArgs e)
        => NavigationService.GoTo(new PilotOptionsScreen());

    private void Plane_Click(object sender, RoutedEventArgs e)
        => NavigationService.GoTo(new PlaneConfigScreen());

    private void Realism_Click(object sender, RoutedEventArgs e)
        => NavigationService.GoTo(new RealismScreen());

    private void Video_Click(object sender, RoutedEventArgs e)
        => NavigationService.GoTo(new VideoOptionsScreen());

    private void Audio_Click(object sender, RoutedEventArgs e)
        => NavigationService.GoTo(new AudioOptionsScreen());

    private void Controls_Click(object sender, RoutedEventArgs e)
        => NavigationService.GoTo(new ControlsScreen());

    private void Network_Click(object sender, RoutedEventArgs e)
        => NavigationService.GoTo(new NetworkOptionsScreen());

    private void Telemetry_Click(object sender, RoutedEventArgs e)
        => NavigationService.GoTo(new TelemetryScreen());

    private void Back_Click(object sender, RoutedEventArgs e)
        => NavigationService.Back();
}
