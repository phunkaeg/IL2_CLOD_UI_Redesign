using System.Windows;
using System.Windows.Controls;
using PlaneLoadoutWpfTest.Services;

namespace PlaneLoadoutWpfTest.Screens;

public partial class LobbyScreen : UserControl
{
    public LobbyScreen()
    {
        InitializeComponent();
    }

    private void Back_Click(object sender, RoutedEventArgs e)
        => NavigationService.Back();

    private void Create_Click(object sender, RoutedEventArgs e)
        => NavigationService.GoTo(new CreateServerScreen());

    // ── Filter popup ──────────────────────────────────────────────────────────

    private void Filters_Click(object sender, RoutedEventArgs e)
        => FiltersOverlay.Visibility = Visibility.Visible;

    private void FiltersClose_Click(object sender, RoutedEventArgs e)
        => FiltersOverlay.Visibility = Visibility.Collapsed;

    private void FiltersBackdrop_Click(object sender, RoutedEventArgs e)
        => FiltersOverlay.Visibility = Visibility.Collapsed;

    private void FiltersApply_Click(object sender, RoutedEventArgs e)
    {
        // TODO: apply filter selections to the lobby list data.
        FiltersOverlay.Visibility = Visibility.Collapsed;
    }
}
