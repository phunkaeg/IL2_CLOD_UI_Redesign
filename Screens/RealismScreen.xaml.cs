using System.Windows;
using System.Windows.Controls;
using PlaneLoadoutWpfTest.Services;

namespace PlaneLoadoutWpfTest.Screens;

public partial class RealismScreen : UserControl
{
    public RealismScreen()
    {
        InitializeComponent();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
        => NavigationService.Back();

    private void Ok_Click(object sender, RoutedEventArgs e)
        => NavigationService.Back();
}
