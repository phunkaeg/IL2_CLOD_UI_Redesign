using System.Windows;
using System.Windows.Controls;
using PlaneLoadoutWpfTest.Services;

namespace PlaneLoadoutWpfTest.Screens;

public partial class PlaneConfigScreen : UserControl
{
    public PlaneConfigScreen()
    {
        InitializeComponent();
    }

    // ── Loaded ────────────────────────────────────────────────────────────────

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        // Seed Country and Squadron lists for the default Army selection (Red).
        RefreshCountryList("Red");

        // Load the Hurricane OBJ model into the 3-D viewer.
        // Path is relative to the output directory (Content item in csproj).
        AircraftViewer.LoadModel(@"Assets\3d\Hurricane_Mk_I_L.obj");
    }

    // ── Army → Country cascade ────────────────────────────────────────────────

    private void ArmyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;

        var army = (ArmyCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
        if (army is null) return;

        RefreshCountryList(army);
    }

    private void RefreshCountryList(string army)
    {
        var countries = DataService.GetCountriesForArmy(army);

        CountryCombo.SelectionChanged -= CountryCombo_SelectionChanged;
        CountryCombo.Items.Clear();

        foreach (var c in countries)
            CountryCombo.Items.Add(new ComboBoxItem { Content = c });

        CountryCombo.SelectedIndex     = 0;
        CountryCombo.SelectionChanged += CountryCombo_SelectionChanged;

        // Trigger squadron refresh for the newly selected country
        RefreshSquadronList(countries.FirstOrDefault() ?? "");
    }

    // ── Country → Squadron cascade ────────────────────────────────────────────

    private void CountryCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;

        var country = (CountryCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
        if (country is null) return;

        RefreshSquadronList(country);
    }

    private void RefreshSquadronList(string country)
    {
        SquadronCombo.Items.Clear();

        foreach (var sq in DataService.GetSquadronsByCountry(country))
            SquadronCombo.Items.Add(new ComboBoxItem { Content = sq.Name });

        if (SquadronCombo.Items.Count > 0)
            SquadronCombo.SelectedIndex = 0;
    }

    // ── Select Aircraft ───────────────────────────────────────────────────────

    private void SelectAircraft_Click(object sender, RoutedEventArgs e)
    {
        // TODO: navigate to an aircraft selection screen that shows the full
        // DataService.GetAllAircraft() list grouped by side/country/role.
        // For now, cycle through a small demo subset.
        var aircraft = DataService.GetAllAircraft()
                                  .OrderBy(a => a.Country)
                                  .ThenBy(a => a.Name)
                                  .ToList();

        var current = SelectedAircraftLabel.Text;
        var idx     = aircraft.FindIndex(a => a.Name == current);
        var next    = aircraft[(idx + 1) % aircraft.Count];

        SelectedAircraftLabel.Text = next.Name;
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    private void Back_Click(object sender, RoutedEventArgs e)
        => NavigationService.Back();

    private void Loadout_Click(object sender, RoutedEventArgs e)
        => NavigationService.GoTo(new PlaneLoadoutScreen());
}
