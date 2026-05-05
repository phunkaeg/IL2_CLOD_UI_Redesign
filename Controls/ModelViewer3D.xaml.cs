using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using HelixToolkit.Wpf;

namespace PlaneLoadoutWpfTest.Controls;

/// <summary>
/// Self-contained 3-D model preview panel built on WPF's Viewport3D.
///
/// Current state  — renders an animated placeholder cube with 3-point studio
///                  lighting and a dark radial backdrop (HDR substitute).
/// Future state   — call <see cref="LoadModel"/> with an OBJ/FBX path to
///                  replace the cube; replace BackdropRect with an HDR
///                  environment sphere when assets are available.
///
/// Interaction:
///   Left-drag  → orbit (azimuth + elevation)
///   Scroll     → dolly (radius)
///   Auto-spin  → 8 °/s when mouse is not held
/// </summary>
public partial class ModelViewer3D : UserControl
{
    // ── DependencyProperties ──────────────────────────────────────────────────

    public static readonly DependencyProperty ModelLabelProperty =
        DependencyProperty.Register(nameof(ModelLabel), typeof(string),
            typeof(ModelViewer3D),
            new PropertyMetadata("AIRCRAFT", OnLabelChanged));

    public static readonly DependencyProperty SubLabelProperty =
        DependencyProperty.Register(nameof(SubLabel), typeof(string),
            typeof(ModelViewer3D),
            new PropertyMetadata("Placeholder model", OnLabelChanged));

    public static readonly DependencyProperty InitialRadiusProperty =
        DependencyProperty.Register(nameof(InitialRadius), typeof(double),
            typeof(ModelViewer3D),
            new PropertyMetadata(2.8));

    public string ModelLabel
    {
        get => (string)GetValue(ModelLabelProperty);
        set => SetValue(ModelLabelProperty, value);
    }

    public string SubLabel
    {
        get => (string)GetValue(SubLabelProperty);
        set => SetValue(SubLabelProperty, value);
    }

    public double InitialRadius
    {
        get => (double)GetValue(InitialRadiusProperty);
        set => SetValue(InitialRadiusProperty, value);
    }

    private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ModelViewer3D v) v.RefreshLabels();
    }

    // ── Camera orbit state ────────────────────────────────────────────────────

    private double _azimuth   =  25.0;   // ° around Y axis
    private double _elevation =  18.0;   // ° above horizon
    private double _radius    =   2.8;   // distance from world origin

    private const double MinElev     = -80.0;
    private const double MaxElev     =  80.0;
    private const double MinRadius   =   0.8;
    private const double MaxRadius   =  12.0;
    private const double AutoRpmDeg  =   8.0;  // degrees per second auto-spin

    // ── Mouse drag ────────────────────────────────────────────────────────────

    private bool  _dragging;
    private Point _lastMouse;

    // ── Render timer ─────────────────────────────────────────────────────────

    private readonly DispatcherTimer _timer;
    private DateTime _lastTick;

    // ── 3-D scene references ──────────────────────────────────────────────────

    // Rotation applied to the model (auto-spin around Y).
    private readonly AxisAngleRotation3D _spinRotation = new(new Vector3D(0, 1, 0), 0);

    // ── Constructor ───────────────────────────────────────────────────────────

    public ModelViewer3D()
    {
        InitializeComponent();

        // Attach the spin transform to the model container.
        ModelContainer.Transform = new RotateTransform3D(_spinRotation);

        // Build the placeholder cube (replaced by LoadModel() later).
        BuildPlaceholderModel();

        // Position the camera for the initial frame.
        UpdateCamera();

        // 16 ms ≈ 60 fps
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += Timer_Tick;
    }

    // ── Loaded / Unloaded ─────────────────────────────────────────────────────

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        _radius = Math.Clamp(InitialRadius, MinRadius, MaxRadius);
        UpdateCamera();
        _lastTick = DateTime.UtcNow;
        _timer.Start();
        RefreshLabels();
    }

    private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        => _timer.Stop();

    // ── Public API — model loading ────────────────────────────────────────────

    /// <summary>
    /// Loads an OBJ (+ companion MTL) model from <paramref name="filePath"/>
    /// and replaces the placeholder cube.  The file can be an absolute path or
    /// relative to the application's base directory (e.g. "Assets/3d/model.obj").
    ///
    /// Call from the screen's Loaded event so the control is fully measured
    /// before the model is injected.
    /// </summary>
    public void LoadModel(string filePath)
    {
        // Resolve relative paths against the application exe location.
        if (!Path.IsPathRooted(filePath))
            filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filePath);

        if (!File.Exists(filePath))
        {
            PlaceholderNotice.Text = $"[ model asset missing: {Path.GetFileName(filePath)} ]";
            PlaceholderNotice.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            var reader = new ObjReader
            {
                DefaultMaterial = BuildFallbackMaterial(),
                IgnoreErrors = true,
                SkipTransparencyValues = true
            };
            var group  = reader.Read(filePath); // returns Model3DGroup with materials applied

            if (group == null || group.Children.Count == 0)
            {
                PlaceholderNotice.Text = $"[ model asset empty: {Path.GetFileName(filePath)} ]";
                PlaceholderNotice.Visibility = Visibility.Visible;
                return;
            }

            // Fit the model into a unit sphere so every asset appears at the
            // same scale regardless of original units/export scale.
            FitToUnitSphere(group);

            ModelContainer.Content     = group;
            PlaceholderNotice.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            PlaceholderNotice.Text = $"[ model load failed: {ex.Message} ]";
            PlaceholderNotice.Visibility = Visibility.Visible;
        }
    }

    // ── Model-fit helper ──────────────────────────────────────────────────────

    /// <summary>
    /// Scales the model group so its bounding sphere has radius 1, and centres
    /// it at the world origin.  This makes the camera orbit math work correctly
    /// regardless of the OBJ file's native units or scale.
    /// </summary>
    private static void FitToUnitSphere(Model3DGroup group)
    {
        var bounds = group.Bounds;
        if (bounds.IsEmpty) return;

        double maxDim = Math.Max(bounds.SizeX, Math.Max(bounds.SizeY, bounds.SizeZ));
        if (maxDim < 1e-6) return;

        double scale = 2.0 / maxDim;    // scale so longest axis = 2 (radius = 1)

        var centre = new Point3D(
            bounds.X + bounds.SizeX / 2,
            bounds.Y + bounds.SizeY / 2,
            bounds.Z + bounds.SizeZ / 2);

        // Translate to origin, then scale.
        var translate = new TranslateTransform3D(-centre.X, -centre.Y, -centre.Z);
        var scaleT    = new ScaleTransform3D(scale, scale, scale);

        var combined = new Transform3DGroup();
        combined.Children.Add(translate);
        combined.Children.Add(scaleT);

        group.Transform = combined;
    }

    // ── Fallback material (used when MTL is missing or a face has none) ───────

    private static MaterialGroup BuildFallbackMaterial()
    {
        var mat = new MaterialGroup();
        mat.Children.Add(new DiffuseMaterial(
            new SolidColorBrush(Color.FromRgb(0x28, 0x3C, 0x48))));
        mat.Children.Add(new SpecularMaterial(
            new SolidColorBrush(Color.FromRgb(0x90, 0xAA, 0xBC)), 80.0));
        return mat;
    }

    // ── Timer → auto-spin + camera update ────────────────────────────────────

    private void Timer_Tick(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        double dt = (now - _lastTick).TotalSeconds;
        _lastTick = now;

        // Only auto-spin when the user isn't dragging.
        if (!_dragging)
        {
            _azimuth = (_azimuth + AutoRpmDeg * dt) % 360.0;
            UpdateCamera();
        }
    }

    // ── Camera ────────────────────────────────────────────────────────────────

    private void UpdateCamera()
    {
        double az = _azimuth   * Math.PI / 180.0;
        double el = _elevation * Math.PI / 180.0;

        double cosEl = Math.Cos(el);
        double x =  _radius * cosEl * Math.Sin(az);
        double y =  _radius * Math.Sin(el);
        double z =  _radius * cosEl * Math.Cos(az);

        Camera.Position      = new Point3D(x, y, z);
        Camera.LookDirection = new Vector3D(-x, -y, -z);
        Camera.UpDirection   = new Vector3D(0, 1, 0);
    }

    // ── Mouse input ───────────────────────────────────────────────────────────

    private void Viewport_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragging  = true;
        _lastMouse = e.GetPosition(this);
        Viewport.CaptureMouse();
        e.Handled = true;
    }

    private void Viewport_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;

        var   pos   = e.GetPosition(this);
        var   delta = pos - _lastMouse;
        _lastMouse  = pos;

        _azimuth   -= delta.X * 0.45;
        _elevation += delta.Y * 0.45;
        _elevation  = Math.Clamp(_elevation, MinElev, MaxElev);

        UpdateCamera();
    }

    private void Viewport_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _dragging = false;
        Viewport.ReleaseMouseCapture();
    }

    private void Viewport_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Each detent = 120 units; step 0.25 per detent.
        _radius -= (e.Delta / 120.0) * 0.25;
        _radius  = Math.Clamp(_radius, MinRadius, MaxRadius);
        UpdateCamera();
        e.Handled = true;
    }

    // ── Labels ────────────────────────────────────────────────────────────────

    private void RefreshLabels()
    {
        ModelLabelBlock.Text = ModelLabel;
        SubLabelBlock.Text   = SubLabel;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Placeholder geometry — CUBE
    // ══════════════════════════════════════════════════════════════════════════

    private void BuildPlaceholderModel()
    {
        var mesh = new MeshGeometry3D();
        const double h = 0.5;   // half-extent — unit cube

        // Six faces, each as a quad with its own flat normal (for sharp edges).
        // Winding is counter-clockwise when viewed from outside.
        AddFace(mesh, new Point3D(-h,-h, h), new Point3D( h,-h, h),
                      new Point3D( h, h, h), new Point3D(-h, h, h),
                      new Vector3D(0, 0, 1));   // Front  (+Z)

        AddFace(mesh, new Point3D( h,-h,-h), new Point3D(-h,-h,-h),
                      new Point3D(-h, h,-h), new Point3D( h, h,-h),
                      new Vector3D(0, 0,-1));   // Back   (-Z)

        AddFace(mesh, new Point3D( h,-h, h), new Point3D( h,-h,-h),
                      new Point3D( h, h,-h), new Point3D( h, h, h),
                      new Vector3D(1, 0, 0));   // Right  (+X)

        AddFace(mesh, new Point3D(-h,-h,-h), new Point3D(-h,-h, h),
                      new Point3D(-h, h, h), new Point3D(-h, h,-h),
                      new Vector3D(-1, 0, 0));  // Left   (-X)

        AddFace(mesh, new Point3D(-h, h, h), new Point3D( h, h, h),
                      new Point3D( h, h,-h), new Point3D(-h, h,-h),
                      new Vector3D(0, 1, 0));   // Top    (+Y)

        AddFace(mesh, new Point3D(-h,-h,-h), new Point3D( h,-h,-h),
                      new Point3D( h,-h, h), new Point3D(-h,-h, h),
                      new Vector3D(0,-1, 0));   // Bottom (-Y)

        // ── Material — dark anodised-aluminium look ───────────────────────
        // The diffuse hue matches the UI's dark blue-teal palette;
        // a high-power specular gives it a metallic sheen under the key light.
        var material = new MaterialGroup();
        material.Children.Add(new DiffuseMaterial(
            new SolidColorBrush(Color.FromRgb(0x28, 0x3C, 0x48))));   // dark steel-teal
        material.Children.Add(new SpecularMaterial(
            new SolidColorBrush(Color.FromRgb(0x90, 0xAA, 0xBC)), 80.0)); // metallic sheen

        ModelContainer.Content = new GeometryModel3D(mesh, material);
    }

    // ── Mesh helper ───────────────────────────────────────────────────────────

    /// <summary>
    /// Adds a quad (two triangles, flat-shaded) to <paramref name="mesh"/>.
    /// Vertices <paramref name="p0"/>→<paramref name="p3"/> must be in
    /// counter-clockwise order when viewed from the outside of the surface.
    /// </summary>
    private static void AddFace(
        MeshGeometry3D mesh,
        Point3D p0, Point3D p1, Point3D p2, Point3D p3,
        Vector3D normal)
    {
        int i = mesh.Positions.Count;

        mesh.Positions.Add(p0);  mesh.Normals.Add(normal);
        mesh.Positions.Add(p1);  mesh.Normals.Add(normal);
        mesh.Positions.Add(p2);  mesh.Normals.Add(normal);
        mesh.Positions.Add(p3);  mesh.Normals.Add(normal);

        mesh.TriangleIndices.Add(i);     mesh.TriangleIndices.Add(i + 1);
        mesh.TriangleIndices.Add(i + 2);
        mesh.TriangleIndices.Add(i);     mesh.TriangleIndices.Add(i + 2);
        mesh.TriangleIndices.Add(i + 3);
    }
}
