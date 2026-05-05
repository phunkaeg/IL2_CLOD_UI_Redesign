using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using PlaneLoadoutWpfTest.Services;

namespace PlaneLoadoutWpfTest.Screens;

public partial class AudioOptionsScreen : UserControl
{
    public AudioOptionsScreen()
    {
        InitializeComponent();
    }

    private void MasterSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (MasterLabel != null) MasterLabel.Text = $"{(int)e.NewValue}%";
    }

    private void EffectsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (EffectsLabel != null) EffectsLabel.Text = $"{(int)e.NewValue}%";
    }

    private void MusicSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (MusicLabel != null) MusicLabel.Text = $"{(int)e.NewValue}%";
    }

    private void VoiceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (VoiceLabel != null) VoiceLabel.Text = $"{(int)e.NewValue}%";
    }

    private void CockpitSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (CockpitLabel != null) CockpitLabel.Text = $"{(int)e.NewValue}%";
    }

    private void WindSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (WindLabel != null) WindLabel.Text = $"{(int)e.NewValue}%";
    }

    private void Back_Click(object sender, RoutedEventArgs e)
        => NavigationService.Back();

    private void Apply_Click(object sender, RoutedEventArgs e)
        => NavigationService.Back();
}
