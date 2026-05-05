using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PlaneLoadoutWpfTest;

/// <summary>
/// Top-down convergence diagram for the Hurricane's 8 × .303 wing guns.
/// Draws the user's hurricane silhouette image, 8 solid convergence beams
/// that meet at the impact point, then dashed extensions past the crossing.
/// </summary>
public sealed class ConvergenceDiagram : FrameworkElement
{
    // ── Frozen static resources ──────────────────────────────────────────────
    private static readonly Brush s_bg         = Freeze(new SolidColorBrush(Color.FromArgb(18,  255, 255, 255)));
    private static readonly Pen   s_grid       = Freeze(new Pen(new SolidColorBrush(Color.FromArgb(25,  160, 142,  97)), 1.0));
    private static readonly Pen   s_beamSolid  = Freeze(new Pen(new SolidColorBrush(Color.FromArgb(200, 199, 164,  96)), 1.4));
    private static readonly Pen   s_beamDash   = MakeDashPen(Color.FromArgb(85,  199, 164,  96), 1.0, 5, 4);
    private static readonly Pen   s_crosshair  = Freeze(new Pen(new SolidColorBrush(Color.FromArgb(38,  199, 164,  96)), 1.0));
    private static readonly Brush s_marker     = Freeze(new SolidColorBrush(Color.FromRgb(229, 197, 118)));
    private static readonly Pen   s_markerPen  = Freeze(new Pen(new SolidColorBrush(Color.FromRgb(229, 197, 118)), 1.0));
    private static readonly Brush s_text       = Freeze(new SolidColorBrush(Color.FromRgb(235, 226, 213)));
    private static readonly Brush s_dimText    = Freeze(new SolidColorBrush(Color.FromArgb(130, 235, 226, 213)));

    private static T Freeze<T>(T f) where T : Freezable { f.Freeze(); return f; }

    private static Pen MakeDashPen(Color color, double thickness, double on, double off)
    {
        var pen = new Pen(new SolidColorBrush(color), thickness);
        pen.DashStyle = new DashStyle(new double[] { on, off }, 0);
        pen.Freeze();
        return pen;
    }

    // ── Hurricane silhouette image (lazy, loaded once on first render) ───────
    private static readonly Lazy<ImageSource?> s_silhouette = new(() =>
    {
        try
        {
            var bi = new BitmapImage(
                new Uri("pack://application:,,,/Assets/Hurricane_Silouhette2.png"));
            bi.Freeze();
            return bi;
        }
        catch { return null; }
    });

    // ── Dependency properties ────────────────────────────────────────────────
    public static readonly DependencyProperty HorizontalConvergenceProperty =
        DependencyProperty.Register(nameof(HorizontalConvergence), typeof(int), typeof(ConvergenceDiagram),
            new FrameworkPropertyMetadata(300, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty VerticalConvergenceProperty =
        DependencyProperty.Register(nameof(VerticalConvergence), typeof(int), typeof(ConvergenceDiagram),
            new FrameworkPropertyMetadata(300, FrameworkPropertyMetadataOptions.AffectsRender));

    public int HorizontalConvergence
    {
        get => (int)GetValue(HorizontalConvergenceProperty);
        set => SetValue(HorizontalConvergenceProperty, value);
    }

    public int VerticalConvergence
    {
        get => (int)GetValue(VerticalConvergenceProperty);
        set => SetValue(VerticalConvergenceProperty, value);
    }

    protected override Size MeasureOverride(Size available)
    {
        double w = double.IsInfinity(available.Width)  ? 400 : available.Width;
        double h = double.IsInfinity(available.Height) ? 200 : available.Height;
        return new Size(w, h);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w < 40 || h < 40) return;

        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        // ── Background ───────────────────────────────────────────────────────
        dc.DrawRectangle(s_bg, null, new Rect(0, 0, w, h));

        // ── Grid ─────────────────────────────────────────────────────────────
        for (int i = 1; i < 8; i++)
            dc.DrawLine(s_grid, new Point(i * w / 8.0, 0), new Point(i * w / 8.0, h));
        for (int i = 1; i < 4; i++)
            dc.DrawLine(s_grid, new Point(0, i * h / 4.0), new Point(w, i * h / 4.0));

        // ── Layout ───────────────────────────────────────────────────────────
        double cy = h * 0.50;
        double cx = Math.Max(16, w * 0.24 - 100.0);   // gun-muzzle / wing-leading-edge X

        // 4 gun Y-offsets per wing (innermost → outermost)
        double[] offsets = { h * 0.105, h * 0.185, h * 0.265, h * 0.355 };
        double outerSpan = offsets[^1];

        // ── Hurricane silhouette ─────────────────────────────────────────────
        var sil = s_silhouette.Value;
        if (sil != null)
        {
            double natW = sil.Width;
            double natH = sil.Height;

            // Always scale so the VERTICAL extent (wingspan) fills 2×outerSpan
            // with a 28 % margin.  This works for both portrait and landscape
            // source images because we map the image height → diagram Y axis.
            double drawH = outerSpan * 2 * 1.28;
            double drawW = drawH * natW / natH;

            // Wing leading edge sits at ~32 % from the left of the image when
            // the nose faces right.  Aligning that point to cx places the gun
            // muzzles exactly over the beam origins.
            double imgX = cx - drawW * 0.32 - 33.0;
            double imgY = cy - drawH / 2.0;

            dc.PushOpacity(0.55);
            dc.DrawImage(sil, new Rect(imgX, imgY, drawW, drawH));
            dc.Pop();   // opacity
        }

        // ── Impact point ─────────────────────────────────────────────────────
        double hT   = Math.Clamp((HorizontalConvergence - 50.0) / 750.0, 0, 1);
        double impX = Math.Max(cx + 40, w * 0.48 + hT * (w * 0.42));

        double impY = cy;

        var impact = new Point(impX, impY);

        // ── Crosshair through impact ──────────────────────────────────────────
        dc.DrawLine(s_crosshair, new Point(cx,    impY), new Point(w,    impY));
        dc.DrawLine(s_crosshair, new Point(impX,  0),   new Point(impX, h));

        // ── 8 gun beams ───────────────────────────────────────────────────────
        foreach (double o in offsets)
        {
            foreach (double sign in new[] { -1.0, 1.0 })
            {
                var gun = new Point(cx, cy + sign * o);

                // Solid line gun → impact
                dc.DrawLine(s_beamSolid, gun, impact);

                // Dashed extension past the crossing point
                double dx  = impact.X - gun.X;
                double dy  = impact.Y - gun.Y;
                double len = Math.Sqrt(dx * dx + dy * dy);
                if (len > 1)
                {
                    double ext = Math.Min(len * 0.55, w * 0.22);
                    dc.DrawLine(s_beamDash, impact,
                        new Point(impact.X + dx / len * ext,
                                  impact.Y + dy / len * ext));
                }
            }
        }

        // ── Impact marker ────────────────────────────────────────────────────
        dc.DrawEllipse(s_marker, s_markerPen, impact, 5, 5);

        // ── Range labels ────────────────────────────────────────────────────
        DrawText(dc, "50",     s_dimText, dpi, new Point(w * 0.30, h - 15), 10);
        DrawText(dc, "800 yd", s_dimText, dpi, new Point(w * 0.88, h - 15), 10);

        double labelX = Math.Min(w - 82, impX + 9);
        double labelY = Math.Max(4, impY - 30);
        DrawText(dc, $"H  {HorizontalConvergence} yd", s_text, dpi, new Point(labelX, labelY));
        DrawText(dc, $"V  {VerticalConvergence} yd",   s_text, dpi, new Point(labelX, labelY + 14));
    }

    private static void DrawText(DrawingContext dc, string text, Brush brush,
        double dpi, Point origin, double size = 11.5)
    {
        var ft = new FormattedText(text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), size, brush, dpi);
        dc.DrawText(ft, origin);
    }
}
