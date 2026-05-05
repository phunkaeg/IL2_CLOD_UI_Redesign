using System.Windows;
using System.Windows.Controls;
using PlaneLoadoutWpfTest.Services;

namespace PlaneLoadoutWpfTest.Screens;

public partial class ExtrasMenuScreen : UserControl
{
    public ExtrasMenuScreen()
    {
        InitializeComponent();
    }

    private void Statistics_Click(object sender, RoutedEventArgs e)
        => NavigationService.GoTo(new StatisticsScreen());

    private void Credits_Click(object sender, RoutedEventArgs e)
        => NavigationService.GoTo(new CreditsScreen());

    private void Back_Click(object sender, RoutedEventArgs e)
        => NavigationService.Back();
}
