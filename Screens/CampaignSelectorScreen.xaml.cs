using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using PlaneLoadoutWpfTest.Services;

namespace PlaneLoadoutWpfTest.Screens;

public partial class CampaignSelectorScreen : UserControl
{
    private readonly List<CampaignSelectionItem> _campaigns = [];
    private string _theaterFilter = "All";

    public CampaignSelectorScreen()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadCampaignsAsync(false);
    }

    private async Task LoadCampaignsAsync(bool forceRefresh)
    {
        var useCached = !forceRefresh && CampaignSelectionService.HasCache;
        StatusText.Text = useCached ? "Using cached campaign list..." : "Scanning campaign folders...";
        SetLoading(!useCached);
        NextButton.IsEnabled = false;
        SelectedPathText.Text = "";
        CampaignChoiceList.ItemsSource = null;
        _campaigns.Clear();

        IReadOnlyList<CampaignSelectionItem> campaigns = [];
        try
        {
            campaigns = await Task.Run(() => CampaignSelectionService.DiscoverCampaigns(forceRefresh));
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Campaign scan failed: {ex.Message}";
            if (!useCached) SetLoading(false);
            return;
        }
        finally
        {
            if (!useCached) SetLoading(false);
        }

        _campaigns.AddRange(campaigns);
        ApplyTheaterFilter();
    }

    private async void Rescan_Click(object sender, RoutedEventArgs e)
        => await LoadCampaignsAsync(true);

    private void TheaterFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag }) return;
        _theaterFilter = tag;
        ApplyTheaterFilter();
    }

    private void ApplyTheaterFilter()
    {
        TheaterAllButton.Style = (Style)FindResource(_theaterFilter == "All" ? "OkButton" : typeof(Button));
        TheaterStraitButton.Style = (Style)FindResource(_theaterFilter == "Strait of Dover" ? "OkButton" : typeof(Button));
        TheaterTobrukButton.Style = (Style)FindResource(_theaterFilter == "Tobruk" ? "OkButton" : typeof(Button));

        var filtered = _theaterFilter == "All"
            ? _campaigns
            : _campaigns.Where(c => c.Theater.Equals(_theaterFilter, StringComparison.OrdinalIgnoreCase)).ToList();

        CampaignChoiceList.ItemsSource = filtered;
        StatusText.Text = _campaigns.Count == 0
            ? "No playable campaign folders detected."
            : _theaterFilter == "All"
                ? $"{_campaigns.Count} campaigns detected"
                : $"{filtered.Count} {_theaterFilter} campaigns / {_campaigns.Count} total";

        NextButton.IsEnabled = false;
        SelectedPathText.Text = "";
        if (filtered.Count > 0)
            CampaignChoiceList.SelectedIndex = 0;
    }

    private void CampaignChoiceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var item = CampaignChoiceList.SelectedItem as CampaignSelectionItem;
        NextButton.IsEnabled = item is not null;
        SelectedPathText.Text = item?.Root ?? "";
        ShowCampaignDetails(item);
    }

    private void CampaignChoiceList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        => OpenSelectedCampaign();

    private void Next_Click(object sender, RoutedEventArgs e)
        => OpenSelectedCampaign();

    private void OpenSelectedCampaign()
    {
        if (CampaignChoiceList.SelectedItem is not CampaignSelectionItem item) return;
        NavigationService.GoTo(new CampaignScreen(item.Root, item.Campaign));
    }

    private void Back_Click(object sender, RoutedEventArgs e)
        => NavigationService.Back();

    private void ShowCampaignDetails(CampaignSelectionItem? item)
    {
        if (item is null)
        {
            CampaignPosterImage.Source = null;
            NoCampaignPosterHint.Visibility = Visibility.Visible;
            CampaignDetailTitle.Text = "";
            CampaignDetailMeta.Text = "";
            CampaignDescriptionText.Text = "";
            return;
        }

        CampaignDetailTitle.Text = item.Title;
        CampaignDetailMeta.Text = string.Join(" / ", new[] { item.Theater, item.Period, item.Summary }.Where(s => !string.IsNullOrWhiteSpace(s)));
        CampaignDescriptionText.Text = item.Description;
        CampaignPosterImage.Source = MakeImage(item.PosterImage);
        NoCampaignPosterHint.Visibility = CampaignPosterImage.Source is null ? Visibility.Visible : Visibility.Collapsed;
    }

    private static BitmapImage? MakeImage(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            var uri = path.StartsWith("/", StringComparison.Ordinal)
                ? new Uri(path, UriKind.Relative)
                : new Uri(path, UriKind.Absolute);
            return new BitmapImage(uri);
        }
        catch
        {
            return null;
        }
    }

    private void SetLoading(bool isLoading)
    {
        LoadingOverlay.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
        LoadingOverlayText.Text = isLoading
            ? "Scanning campaign folders, briefings and mission files..."
            : "";
    }
}
