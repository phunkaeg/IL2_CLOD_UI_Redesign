using System.Windows;
using System.Windows.Controls;
using PlaneLoadoutWpfTest.Services;

namespace PlaneLoadoutWpfTest.Screens;

public partial class VideoOptionsScreen : UserControl
{
    public VideoOptionsScreen()
    {
        InitializeComponent();
    }

    private void AaMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        int idx = AaModeCombo.SelectedIndex;
        FxaaPanel.Visibility  = idx == 1 ? Visibility.Visible : Visibility.Collapsed;
        SmaaPanel.Visibility  = idx == 2 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void FxaaQuality_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (FxaaQualityLabel != null)
            FxaaQualityLabel.Text = ((int)e.NewValue).ToString();
    }

    private void ShowAdvanced_Checked(object sender, RoutedEventArgs e)
        => AdvancedPanel.Visibility = Visibility.Visible;

    private void ShowAdvanced_Unchecked(object sender, RoutedEventArgs e)
        => AdvancedPanel.Visibility = Visibility.Collapsed;

    private void Cancel_Click(object sender, RoutedEventArgs e)
        => NavigationService.Back();

    private void Ok_Click(object sender, RoutedEventArgs e)
        => NavigationService.Back();
}
