using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PlaneLoadoutWpfTest.Services;

namespace PlaneLoadoutWpfTest.Screens;

public partial class TelemetryScreen : UserControl
{
    // ── State ─────────────────────────────────────────────────────────────────

    // Crash telemetry: null = not set, true = enabled, false = declined
    private bool? _telemetryEnabled = null;

    // Motion platform
    private bool _motionEnabled = false;

    public TelemetryScreen()
    {
        InitializeComponent();
        UpdateCrashLabel();
        UpdateMotionUi();
    }

    // ── Crash reporting ───────────────────────────────────────────────────────

    private void UpdateCrashLabel()
    {
        CurrentSettingLabel.Text = _telemetryEnabled switch
        {
            true  => "ENABLED",
            false => "DECLINED",
            null  => "NOT SET"
        };
        CurrentSettingLabel.Foreground = _telemetryEnabled switch
        {
            true  => (Brush)FindResource("TextGoldBrush"),
            false => (Brush)FindResource("DangerBrush"),
            null  => (Brush)FindResource("TextDimBrush")
        };
    }

    private void Yes_Click(object sender, RoutedEventArgs e)
    {
        _telemetryEnabled = true;
        UpdateCrashLabel();
    }

    private void No_Click(object sender, RoutedEventArgs e)
    {
        _telemetryEnabled = false;
        UpdateCrashLabel();
    }

    // ── Motion platform ───────────────────────────────────────────────────────

    private void UpdateMotionUi()
    {
        MotionStatusLabel.Text       = _motionEnabled ? "ENABLED" : "DISABLED";
        MotionStatusLabel.Foreground = _motionEnabled
            ? (Brush)FindResource("TextGoldBrush")
            : (Brush)FindResource("TextDimBrush");

        MotionSettingsPanel.IsEnabled = _motionEnabled;
        MotionSettingsPanel.Opacity   = _motionEnabled ? 1.0 : 0.45;
        MotionTestBtn.IsEnabled       = _motionEnabled;
    }

    private void MotionEnable_Click(object sender, RoutedEventArgs e)
    {
        _motionEnabled = true;
        MotionTestLabel.Text = "";
        UpdateMotionUi();
    }

    private void MotionDisable_Click(object sender, RoutedEventArgs e)
    {
        _motionEnabled = false;
        MotionTestLabel.Text = "";
        UpdateMotionUi();
    }

    /// <summary>
    /// Sends a single test UDP packet to the configured IP:port and reports
    /// success or failure.  The packet contains the ASCII string "CLOD_TEST".
    /// </summary>
    private async void MotionTest_Click(object sender, RoutedEventArgs e)
    {
        MotionTestLabel.Text       = "Testing…";
        MotionTestLabel.Foreground = (Brush)FindResource("TextDimBrush");
        MotionTestBtn.IsEnabled    = false;

        var ip   = MotionIpBox.Text.Trim();
        var portText = MotionPortBox.Text.Trim();

        if (!IPAddress.TryParse(ip, out _) || !int.TryParse(portText, out int port)
            || port < 1 || port > 65535)
        {
            MotionTestLabel.Text       = "✗  Invalid IP address or port number.";
            MotionTestLabel.Foreground = (Brush)FindResource("DangerBrush");
            MotionTestBtn.IsEnabled    = true;
            return;
        }

        try
        {
            using var udp     = new UdpClient();
            var       payload = Encoding.ASCII.GetBytes("CLOD_TEST");
            await udp.SendAsync(payload, payload.Length, ip, port);

            MotionTestLabel.Text       = $"✓  Test packet sent to {ip}:{port}. Check your platform software for reception.";
            MotionTestLabel.Foreground = (Brush)FindResource("TextGoldBrush");
        }
        catch (Exception ex)
        {
            MotionTestLabel.Text       = $"✗  Send failed: {ex.Message}";
            MotionTestLabel.Foreground = (Brush)FindResource("DangerBrush");
        }

        MotionTestBtn.IsEnabled = true;
    }

    // ── Footer ────────────────────────────────────────────────────────────────

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        // TODO: persist settings to config file / registry
        NavigationService.Back();
    }

    private void Back_Click(object sender, RoutedEventArgs e)
        => NavigationService.Back();
}
