using System.Windows;
using System.Windows.Controls;
using PlaneLoadoutWpfTest.Services;

namespace PlaneLoadoutWpfTest.Screens;

public partial class CreditsScreen : UserControl
{
    public CreditsScreen()
    {
        InitializeComponent();
    }

    private void Back_Click(object sender, RoutedEventArgs e)
        => NavigationService.Back();
}
