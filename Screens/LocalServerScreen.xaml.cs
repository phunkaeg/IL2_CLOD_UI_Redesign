using System.Windows;
using System.Windows.Controls;
using PlaneLoadoutWpfTest.Services;

namespace PlaneLoadoutWpfTest.Screens;

public partial class LocalServerScreen : UserControl
{
    public LocalServerScreen()
    {
        InitializeComponent();
    }

    private void Back_Click(object sender, RoutedEventArgs e)
        => NavigationService.Back();

    private void NetOptions_Click(object sender, RoutedEventArgs e)
        => NavigationService.GoTo(new NetworkOptionsScreen());

    private void Realism_Click(object sender, RoutedEventArgs e)
        => NavigationService.GoTo(new RealismScreen());
}
