using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PlaneLoadoutWpfTest;

/// <summary>
/// Aircraft summary diagram for the Weapon Sets tab.
/// Renders the Hurricane top-down silhouette with eight amber vertical bars
/// marking the four Browning .303 gun positions on each wing, matching the
/// style seen in the reference IL-2 loadout screen.
/// </summary>
public sealed class GunPositionDiagram : FrameworkElement
{
    // ── Frozen static brushes / pens ─────────────────────────────────────────
    private static readonly Brush s_bg =
        Freeze(new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)));          // transparent

    // Gun bars — semi-transparent amber fill + bright amber outline
    private static readonly Brush s_gunFill =
        Freeze(new SolidColorBrush(Color.FromArgb(110, 199, 164,  96)));
    private static readonly Pen   s_gunPen  =
        Freeze(new Pen(new SolidColorBrush(Color.FromArgb(210, 229, 197, 118)), 1.2));

    // Subtle grid
    private static readonly Pen   s_grid    =
        Freeze(new Pen(new SolidColorBrush(Color.FromArgb(20, 160, 142, 97)), 1.0));

    private static T Freeze<T>(T f) where T : Freezable { f.Freeze(); return f; }

    // ── Silhouette (shared lazy load) ────────────────────────────────────────
    private static readonly Lazy<ImageSource?> s_silhouette = new(() =>
    {
        try
        {
            var bi = new BitmapImage(
                new Uri("pack://application:,,,/Assets/hurricane_top_silhouette.png"));
            bi.Freeze();
            return bi;
        }
        catch { return null; }
    });

    // ── Gun-position fractions ───────────────────────────────────────────────
    // Four guns per wing: inboard → outboard, as fractions of the visual
    // half-wingspan measured from the fuselage centreline.
    // Based on actual Hurricane Mk.I gun station spacing (2.5, 4.5, 7.5, 9.5 ft
    // from wing root on a ~20 ft half-span).
    private static readonly double[] s_gunFracs = { 0.21, 0.32, 0.46, 0.57 };

    // ── Layout constants ─────────────────────────────────────────────────────
    // Fraction of drawH from the top of the silhouette rect where the wing
    // chord centre (gun station) sits.
    private const double WingChordFrac    = 0.44;

    // Half-wingspan as a fraction of drawW.
    private const double HalfSpanFrac     = 0.44;

    // Fuselage half-width as a fraction of drawW (inner gun exclusion zone).
    private const double FuseHalfWidFrac  = 0.09;

    // ── MeasureOverride ──────────────────────────────────────────────────────
    protected override Size MeasureOverride(Size available)
    {
        double w = double.IsInfinity(available.Width)  ? 280 : available.Width;
        double h = double.IsInfinity(available.Height) ? 200 : available.Height;
        return new Size(w, h);
    }

    // ── Render ───────────────────────────────────────────────────────────────
    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w < 40 || h < 40) return;

        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        // Subtle background fill
        dc.DrawRectangle(s_bg, null, new Rect(0, 0, w, h));

        // Optional light grid
        for (int i = 1; i < 6; i++)
            dc.DrawLine(s_grid, new Point(i * w / 6.0, 0), new Point(i * w / 6.0, h));
        for (int i = 1; i < 4; i++)
            dc.DrawLine(s_grid, new Point(0, i * h / 4.0), new Point(w, i * h / 4.0));

        // ── Silhouette ───────────────────────────────────────────────────────
        var sil = s_silhouette.Value;
        if (sil == null) return;

        double natW = sil.Width, natH = sil.Height;

        // Scale to fill 84 % of the control while preserving aspect ratio.
        double scale  = Math.Min(w * 0.84 / natW, h * 0.90 / natH);
        double drawW  = natW * scale;
        double drawH  = natH * scale;
        double silX   = (w - drawW) / 2.0;
        double silY   = (h - drawH) / 2.0;

        dc.PushOpacity(0.55);
        dc.DrawImage(sil, new Rect(silX, silY, drawW, drawH));
        dc.Pop();

        // ── Gun position bars ────────────────────────────────────────────────
        // Vertical bars (parallel to the fuselage axis) at each gun station,
        // mirrored on both wings.
        double wingY      = silY + drawH * WingChordFrac;   // wing chord centre
        double halfSpan   = drawW * HalfSpanFrac;            // visual half-span
        double fuseHW     = drawW * FuseHalfWidFrac;         // fuselage half-width

        double barH = drawH * 0.30;                          // bar height (spans wing chord)
        double barW = Math.Max(5.5, w * 0.020);             // bar width  (≈6 px at 290 px wide)
        double yTop = wingY - barH / 2.0;

        foreach (double frac in s_gunFracs)
        {
            // Absolute X offset from panel centre for this gun position
            double xOff = fuseHW + frac * (halfSpan - fuseHW);

            // Left wing
            dc.DrawRectangle(s_gunFill, s_gunPen,
                new Rect(w / 2.0 - xOff - barW / 2.0, yTop, barW, barH));

            // Right wing (mirrored)
            dc.DrawRectangle(s_gunFill, s_gunPen,
                new Rect(w / 2.0 + xOff - barW / 2.0, yTop, barW, barH));
        }
    }
}
