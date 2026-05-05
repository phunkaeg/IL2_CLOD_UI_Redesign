using System.Windows;
using System.Windows.Controls;
using PlaneLoadoutWpfTest.Services;

namespace PlaneLoadoutWpfTest.Screens;

public partial class CreateServerScreen : UserControl
{
    public CreateServerScreen()
    {
        InitializeComponent();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
        => NavigationService.Back();

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        // TODO: pass configuration to the game engine / server process.
        // For now just navigate back to the lobby list.
        NavigationService.Back();
    }
}
