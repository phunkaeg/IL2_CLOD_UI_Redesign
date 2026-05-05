using System.Windows;
using System.Windows.Controls;
using PlaneLoadoutWpfTest.Services;

namespace PlaneLoadoutWpfTest.Screens;

public partial class TrainingScreen : UserControl
{
    private static readonly (string Title, string Desc)[] Missions =
    [
        ("Introduction to Flight",    "Learn the fundamentals of flight in the Hawker Hurricane. This mission covers the basic controls, attitude, and how to maintain level flight. Suitable for complete beginners."),
        ("Take-off",                  "Master the take-off procedure including engine run-up, throttle management, and directional control on the runway. Covers tail-up technique and rotation speed."),
        ("Landing",                   "Practice circuit pattern and approach procedures. Learn to judge glide path, speed control, and the three-point landing technique."),
        ("First Solo",                "A complete solo flight from engine start to shutdown, combining everything covered in the previous missions. Your first true test of airmanship."),
        ("Spin — Entry and Recovery", "Understand the conditions that lead to a spin and practice the recovery technique. Essential knowledge for all pilots.")
    ];

    public TrainingScreen()
    {
        InitializeComponent();
    }

    private void MissionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Guard: fires during InitializeComponent before named elements exist
        if (MissionTitle == null || MissionDesc == null) return;
        var idx = MissionList.SelectedIndex;
        if (idx < 0 || idx >= Missions.Length) return;
        MissionTitle.Text = Missions[idx].Title;
        MissionDesc.Text  = Missions[idx].Desc;
    }

    private void PlayerPlane_Click(object sender, RoutedEventArgs e)
        => NavigationService.GoTo(new PlaneConfigScreen());

    private void Back_Click(object sender, RoutedEventArgs e)
        => NavigationService.Back();
}
