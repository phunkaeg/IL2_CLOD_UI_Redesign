using System.Windows;
using System.Windows.Controls;
using PlaneLoadoutWpfTest.Services;

namespace PlaneLoadoutWpfTest.Screens;

public partial class NetworkOptionsScreen : UserControl
{
    public NetworkOptionsScreen()
    {
        InitializeComponent();
    }

    private void Back_Click(object sender, RoutedEventArgs e)
        => NavigationService.Back();

    private void Apply_Click(object sender, RoutedEventArgs e)
        => NavigationService.Back();
}
