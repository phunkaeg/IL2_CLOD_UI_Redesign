using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PlaneLoadoutWpfTest;

public partial class DrumSpinner : UserControl
{
    private const double SlotH = 36.0;
    private readonly TranslateTransform _t = new();

    // Generation counter — ensures only the most recently started animation's
    // Completed callback calls SetInstant (prevents stale callbacks on rapid scroll).
    private int _animGen = 0;

    // ── Dependency properties ────────────────────────────────────────────────

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(int), typeof(DrumSpinner),
            new FrameworkPropertyMetadata(0,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnValueChanged));

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(nameof(Minimum), typeof(int), typeof(DrumSpinner),
            new PropertyMetadata(0));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(nameof(Maximum), typeof(int), typeof(DrumSpinner),
            new PropertyMetadata(999));

    public static readonly DependencyProperty StepProperty =
        DependencyProperty.Register(nameof(Step), typeof(int), typeof(DrumSpinner),
            new PropertyMetadata(1));

    public int Value
    {
        get => (int)GetValue(ValueProperty);
        set => SetValue(ValueProperty, Math.Clamp(value, Minimum, Maximum));
    }

    public int Minimum { get => (int)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public int Maximum { get => (int)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public int Step    { get => (int)GetValue(StepProperty);    set => SetValue(StepProperty, value); }

    // ── Init ─────────────────────────────────────────────────────────────────

    public DrumSpinner()
    {
        InitializeComponent();
        SlidePanel.RenderTransform = _t;
        Loaded += (_, _) => SetInstant(Value);
    }

    // ── Value change ─────────────────────────────────────────────────────────

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DrumSpinner drum) return;
        var oldVal = (int)e.OldValue;
        var newVal = (int)e.NewValue;
        if (!drum.IsLoaded) { drum.SetInstant(newVal); return; }
        drum.Animate(oldVal, newVal);
    }

    /// <summary>
    /// Two-slot sliding animation.
    ///
    /// UP  (value increases): SlotA = old (top), SlotB = new (bottom).
    ///   Animate Y: 0 → –SlotH — SlotB scrolls up into view.
    ///
    /// DOWN (value decreases): SlotA = new (top), SlotB = old (bottom).
    ///   Animate Y: –SlotH → 0 — SlotA scrolls down into view.
    ///
    /// FillBehavior.Stop is used so the transform always reverts to its base
    /// value when the animation finishes (no "held-end" state to confuse
    /// subsequent animations). The Completed callback then calls SetInstant
    /// to leave both slots identical and Y=0, which hides any snap artefact.
    /// </summary>
    private void Animate(int oldVal, int newVal)
    {
        bool up = newVal > oldVal;
        if (up)
        {
            SlotA.Text = Fmt(oldVal);
            SlotB.Text = Fmt(newVal);
            RunAnim(0.0, -SlotH, newVal);
        }
        else
        {
            SlotA.Text = Fmt(newVal);
            SlotB.Text = Fmt(oldVal);
            RunAnim(-SlotH, 0.0, newVal);
        }
    }

    private void SetInstant(int val)
    {
        SlotA.Text = Fmt(val);
        SlotB.Text = Fmt(val);
        _t.BeginAnimation(TranslateTransform.YProperty, null);
        _t.Y = 0;
    }

    // ── Core animation ────────────────────────────────────────────────────────

    private void RunAnim(double from, double to, int finalValue)
    {
        // Capture a generation token so only the latest completion callback fires.
        int myGen = ++_animGen;

        // ① Kill any in-flight animation — property falls back to base value.
        _t.BeginAnimation(TranslateTransform.YProperty, null);

        // ② Set the base value synchronously.
        //    This also determines where FillBehavior.Stop will revert to.
        _t.Y = from;

        // ③ Build a FillBehavior.Stop animation.
        //    • No 'From' → WPF reads the current base value (= 'from').
        //    • Stop instead of HoldEnd → transform reverts to 'from' after the
        //      animation ends, so there is never a stale held-end position.
        var anim = new DoubleAnimation
        {
            To             = to,
            Duration       = TimeSpan.FromMilliseconds(140),
            FillBehavior   = FillBehavior.Stop,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        // ④ When the animation naturally completes, snap to a clean state.
        //    • Both slots show finalValue, Y=0 → visually identical to the
        //      end frame (SlotB at Y=–SlotH, or SlotA at Y=0), so the revert
        //      from FillBehavior.Stop is invisible.
        anim.Completed += (_, _) =>
        {
            if (_animGen == myGen)          // only if no newer animation started
                SetInstant(finalValue);
        };

        _t.BeginAnimation(TranslateTransform.YProperty, anim);
    }

    private static string Fmt(int v) => v.ToString();

    // ── Interaction ───────────────────────────────────────────────────────────

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        Value = e.Delta > 0
            ? Math.Min(Maximum, Value + Step)
            : Math.Max(Minimum, Value - Step);
        e.Handled = true;
    }

    private void BtnDown_Click(object sender, RoutedEventArgs e) => StepDown();
    private void BtnUp_Click(object sender, RoutedEventArgs e)   => StepUp();

    public void StepUp()   => Value = Math.Min(Maximum, Value + Step);
    public void StepDown() => Value = Math.Max(Minimum, Value - Step);
}
