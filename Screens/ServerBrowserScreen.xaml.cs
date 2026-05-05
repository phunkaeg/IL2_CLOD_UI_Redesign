using System.Windows;
using System.Windows.Controls;
using PlaneLoadoutWpfTest.Services;

namespace PlaneLoadoutWpfTest.Screens;

public partial class ServerBrowserScreen : UserControl
{
    public ServerBrowserScreen()
    {
        InitializeComponent();
    }

    private void Back_Click(object sender, RoutedEventArgs e)
        => NavigationService.Back();
}
