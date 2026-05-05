using System.Windows;
using System.Windows.Controls;
using PlaneLoadoutWpfTest.Services;

namespace PlaneLoadoutWpfTest.Screens;

public partial class MultiplayerMenuScreen : UserControl
{
    public MultiplayerMenuScreen()
    {
        InitializeComponent();
    }

    private void Lobby_Click(object sender, RoutedEventArgs e)
        => NavigationService.GoTo(new LobbyScreen());

    private void ServerBrowser_Click(object sender, RoutedEventArgs e)
        => NavigationService.GoTo(new ServerBrowserScreen());

    private void LocalServer_Click(object sender, RoutedEventArgs e)
        => NavigationService.GoTo(new LocalServerScreen());

    private void Back_Click(object sender, RoutedEventArgs e)
        => NavigationService.Back();
}
