using System.Windows;
using System.Windows.Controls;
using PlaneLoadoutWpfTest.Services;

namespace PlaneLoadoutWpfTest.Screens;

public partial class PilotOptionsScreen : UserControl
{
    public PilotOptionsScreen()
    {
        InitializeComponent();
        Loaded += UserControl_Loaded;
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        // Load the pilot OBJ model into the 3-D viewer.
        PilotViewer.LoadModel(@"Assets\3d\ww2_Pilot.obj");
    }

    private void Back_Click(object sender, RoutedEventArgs e)
        => NavigationService.Back();

    private void Save_Click(object sender, RoutedEventArgs e)
        => NavigationService.Back();
}
