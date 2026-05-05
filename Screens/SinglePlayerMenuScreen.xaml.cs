using System.Windows;
using System.Windows.Controls;
using PlaneLoadoutWpfTest.Services;

namespace PlaneLoadoutWpfTest.Screens;

public partial class SinglePlayerMenuScreen : UserControl
{
    public SinglePlayerMenuScreen()
    {
        InitializeComponent();
    }

    private void Training_Click(object sender, RoutedEventArgs e)
        => NavigationService.GoTo(new TrainingScreen());

    private void QuickMission_Click(object sender, RoutedEventArgs e)
        => NavigationService.GoTo(new QuickMissionScreen());

    private void SingleMission_Click(object sender, RoutedEventArgs e)
        => NavigationService.GoTo(new SingleMissionScreen());

    private void Campaign_Click(object sender, RoutedEventArgs e)
        => NavigationService.GoTo(new CampaignSelectorScreen());

    private void Back_Click(object sender, RoutedEventArgs e)
        => NavigationService.Back();
}
