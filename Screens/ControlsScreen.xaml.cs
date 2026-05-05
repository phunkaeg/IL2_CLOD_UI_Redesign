using System.Globalization;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using PlaneLoadoutWpfTest.Services;

namespace PlaneLoadoutWpfTest.Screens;

// ── Value converters ──────────────────────────────────────────────────────────

/// <summary>Returns Visible when the bound ICollection has at least one item.</summary>
public sealed class AnyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is System.Collections.ICollection { Count: > 0 }
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>Returns Visible when the bound ICollection is empty (zero items).</summary>
public sealed class EmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is System.Collections.ICollection { Count: 0 }
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

// ── Data models ───────────────────────────────────────────────────────────────

public sealed class ControlEntry : INotifyPropertyChanged
{
    public string Action { get; init; } = "";
    public List<string> Bindings { get; init; } = [];
    public bool IsSeparator { get; init; }
    public bool IsAxis { get; init; }
    public string SourceCategory { get; init; } = "";
    public string Group { get; init; } = "General";
    public bool IsEssential { get; init; }
    public string EngineMode { get; init; } = "All"; // All | Single | Multi
    public int PreviewPercent { get; init; } = 55;
    public bool IsIncremental { get; init; }

    private string _activeBinding = "";

    public string Binding1 => Bindings.Count > 0 ? Bindings[0] : "—";
    public string Binding2 => Bindings.Count > 1 ? Bindings[1] : "—";
    public string Binding3 => Bindings.Count > 2 ? Bindings[2] : "—";
    public string Binding4 => Bindings.Count > 3 ? Bindings[3] : "—";
    public bool Binding1Active => Binding1 == _activeBinding;
    public bool Binding2Active => Binding2 == _activeBinding;
    public bool Binding3Active => Binding3 == _activeBinding;
    public bool Binding4Active => Binding4 == _activeBinding;

    public void SetActiveBinding(string binding)
    {
        if (_activeBinding == binding) return;
        _activeBinding = binding;
        OnPropertyChanged(nameof(Binding1Active));
        OnPropertyChanged(nameof(Binding2Active));
        OnPropertyChanged(nameof(Binding3Active));
        OnPropertyChanged(nameof(Binding4Active));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class ControlCategory
{
    public string Name { get; init; } = "";
    public List<ControlEntry> Entries { get; init; } = [];
    public List<ControlGroup> Groups { get; init; } = [];
    public bool IsAxis { get; init; }
}

public sealed class ControlGroup
{
    public string Name { get; init; } = "";
    public List<ControlEntry> Entries { get; init; } = [];
}

// ── CSV data and parser ───────────────────────────────────────────────────────

file static class ControlsData
{
    // Full controls CSV embedded verbatim (source: Team Fusion Controls.csv)
    private const string Csv = """
CONTROLS,Category,Action,Keybind
Keys,Game,Activate Console,
,,Paste Console,
,,Exit Game,Alt X
,,Exit Game,Alt F4
,,Pause,P
,,Take Screenshot,Print Screen
,,Start/Stop Recording,
,General,Use Item,
,,Fire Current Weapon,Mouse Left
,,Reload,
,,Bail Out,Control E
,,Toggle Canopy / Hatch / Door,Control Space
,,Close Canopy / Hatch / Door,Control O
,,Open Canopy / Hatch / Door,O
,,Jettison Canopy / Hatch / Door,Control J
,,Decrease Primary Illumination,
,,Toggle Primary Cockpit Illumination,Control L
,,Increase Primary Illumination,
,,Decrease Secondary Illumination,
,,Toggle Secondary Cockpit Illumination,Shift L
,,Increase Secondary Illumination,
,,Stopwatch Control,
,,Flight Time Clock Control,
,,Alternate Voltamperemeter Mode,
,,Toggle AI Control,A
,,Map,
,Aircraft,Elevator Up Full,Up
,,Elevator Up One Notch,
,,Elevator Center,
,,Elevator Down One Notch,
,,Elevator Down Full,Down
,,Aileron Left Full,Left
,,Aileron Left One Notch,
,,Aileron Center,
,,Aileron Right One Notch,
,,Aileron Right Full,Right
,,Full Left Rudder,Z
,,Full Left Rudder,Control 7
,,Left Rudder One Notch,
,,Center Rudder,
,,Right Rudder One Notch,
,,Full Right Rudder,X
,,Trim Elevator Nose-up,Shift Up
,,Reset Elevator Trim,
,,Trim Elevator Nose-Down,Shift Down
,,Trim Aileron Left,Shift Left
,,Reset Aileron Trim,
,,Trim Aileron Right,Shift Right
,,Trim Rudder Left,Control Z
,,Reset Rudder Trim,
,,Trim Rudder Right,Control X
,,Toggle Landing Flaps,
,,Lower Landing Flaps Position,V
,,Raise Landing Flaps Position,F
,,Toggle Leading-Edge Slats,
,,Retract Leading-Edge Slats,
,,Extend Leading-Edge Slats,
,,Toggle Undercarriage,G
,,Raise Undercarriage,
,,Lower Undercarriage,
,,Manually Raise Undercarriage,
,,Manually Lower Undercarriage,
,,Engage Undercarriage Emergency System,Control G
,,Full Wheel Brakes,B
,,Full Left Wheel Brake,
,,Full Right Wheel Brake,
,,Toggle Airbrake,
,,Retract Airbrake,
,,Extend Airbrake,
,,Toggle Bomb Bay Doors,Control B
,,Close Bomb Bay Doors,
,,Open Bomb Bay Doors,
,,Toggle Impeller,
,,Close Impeller,
,,Extend Impeller,
,,Toggle Air Filter,
,,Close Air Filter,
,,Open Air Filter,
,,Toggle Air Filter on #1 Engine,
,,Close Air Filter on #1 Engine,
,,Open Air Filter on #1 Engine,
,,Toggle Air Filter on #2 Engine,
,,Close Air Filter on #2 Engine,
,,Open Air Filter on #2 Engine,
,,Landing Lights Toggle,
,,Landing Lights On,
,,Landing Lights Off,
,,Navigation Lights Toggle,
,,Navigation Lights On,
,,Navigation Lights Off,
,,Course Autopilot - Cycle Modes,
,,Course Autopilot - Previous Mode,
,,Course Autopilot - Next Mode,
,,Course Autopilot - Adjust Course Left,
,,Course Autopilot - Adjust Course Right,
,,Course Autopilot - Disable,
,,Fire Guns (Machine Guns),Space
,,Fire Guns (Cannon),
,,Fire Guns (Machine Guns + Cannon),
,,Drop Ordnance (Bombs),
,,Decrease Sight Distance,Control NumPad-2
,,Increase Sight Distance,Control NumPad-8
,,Adjust Sight Left,Control NumPad-4
,,Adjust Sight Right,Control NumPad-6
,,Decrease Sight Altitude,Control NumPad-3
,,Increase Sight Altitude,Control NumPad-9
,,Decrease Sight Velocity,Control NumPad-1
,,Increase Sight Velocity,Control NumPad-7
,,Toggle Bombsight Automation,Control NumPad-5
,,Toggle Gunsight Dimmer,
,,VR Swap Bombsight Eye,
,,Toggle Bombs Armed,Control W
,,Toggle Bomb Distributor Short Delay,
,,Select Previous Bomb Bay,
,,Select Next Bomb Bay,
,,Previous Bomb Bay Distributor Mode,
,,Next Bomb Distributor Mode,
,,Decrease Bomb Distributor Salvo Quantity,
,,Increase Bomb Distributor Salvo Quantity,
,,Decrease Bomb Distributor Delay,
,,Increase Bomb Distributor Delay,
,,Safely Jettison Bombs,
,,Toggle Gunpods,
,,Start Selected Engines,I
,,Magnetos - Previous,
,,Magnetos - Next,
,,Magnetos on #1 Engine - Previous,
,,Magnetos on #1 Engine - Next,
,,Magnetos on #2 Engine - Previous,
,,Magnetos on #2 Engine - Next,
,,Magneto #1 - Off,
,,Magneto #1 - On,
,,Magneto #1 - Toggle,
,,Magneto #2 - Off,
,,Magneto #2 - On,
,,Magneto #2 - Toggle,
,,Magneto #1 on #1 Engine - Off,
,,Magneto #1 on #1 Engine - On,
,,Magneto #1 on #1 Engine - Toggle,
,,Magneto #2 on #1 Engine - Off,
,,Magneto #2 on #1 Engine - On,
,,Magneto #2 on #1 Engine - Toggle,
,,Magneto #1 on #2 Engine - Off,
,,Magneto #1 on #2 Engine - On,
,,Magneto #1 on #2 Engine - Toggle,
,,Magneto #2 on #2 Engine - Off,
,,Magneto #2 on #2 Engine - On,
,,Magneto #2 on #2 Engine - Toggle,
,,Toggle Automatic Radiator Control,R
,,Close Radiator,5
,,Open Radiator,6
,,Radiator #1 - Close,
,,Radiator #1 - Open,
,,Radiator #2 - Close,
,,Radiator #2 - Open,
,,Radiator Cut-off 0 - Toggle,
,,Radiator Cut-off 1 - Toggle,
,,Close Oil Radiator,
,,Open Oil Radiator,
,,Oil Radiator #1 - Close,
,,Oil Radiator #1 - Open,
,,Oil Radiator #2 - Close,
,,Oil Radiator #2 - Open,
,,Fire Extinguisher,9
,,Throttle 0%,Semicolon
,,Throttle 10%,
,,Throttle 20%,
,,Throttle 30%,
,,Throttle 40%,
,,Throttle 50%,
,,Throttle 60%,
,,Throttle 70%,
,,Throttle 80%,
,,Throttle 90%,
,,Throttle 100%,
,,Throttle War Emergency Power,W
,,Decrease Throttle,Underscore
,,Increase Throttle,Equals
,,Toggle War Emergency Power,Backspace
,,Boost Cut-out,T
,,Slow Running Cut-out,Control T
,,#1 Slow Running Cut-out,
,,#2 Slow Running Cut-out,
,,Propeller Pitch 0%,
,,Propeller Pitch 10%,
,,Propeller Pitch 20%,
,,Propeller Pitch 30%,
,,Propeller Pitch 40%,
,,Propeller Pitch 50%,
,,Propeller Pitch 60%,
,,Propeller Pitch 70%,
,,Propeller Pitch 80%,
,,Propeller Pitch 90%,
,,Propeller Pitch 100%,
,,Decrease Propeller Pitch,
,,Increase Propeller Pitch,
,,Propeller Pitch Mode - Decrease,
,,Propeller Pitch Mode - Toggle,
,,Propeller Pitch Mode - Increase,
,,Feather Propeller,
,,#1 Propeller Pitch - Decrease,
,,#1 Propeller Pitch - Increase,
,,#2 Propeller Pitch - Decrease,
,,#2 Propeller Pitch - Increase,
,,Mixture Lean,
,,Mixture 10%,
,,Mixture 20%,
,,Mixture 30%,
,,Mixture 40%,
,,Mixture 50%,
,,Mixture 60%,
,,Mixture 70%,
,,Mixture 80%,
,,Mixture 90%,
,,Mixture 100%,
,,Mixture Rich,
,,Decrease Mixture,
,,Increase Mixture,
,,Selected Carburettor Heat - Decrease,
,,Selected Carburettor Heat - Increase,
,,Selected Carburettor Heat - Toggle,
,,#1 Carburettor Heat - Decrease,
,,#1 Carburettor Heat - Increase,
,,#1 Carburettor Heat - Toggle,
,,#2 Carburettor Heat - Decrease,
,,#2 Carburettor Heat - Increase,
,,#2 Carburettor Heat - Toggle,
,,Selected Supercharger - Previous Step,
,,Selected Supercharger - Next Step,
,,Selected Supercharger - Cycle,
,,#1 Supercharger - Previous Step,
,,#1 Supercharger - Next Step,
,,#1 Supercharger - Cycle,
,,#2 Supercharger - Previous Step,
,,#2 Supercharger - Next Step,
,,#2 Supercharger - Cycle,
,,Fuel Cock #1 - Prev. Position,
,,Fuel Cock #1 - Next Position,
,,Fuel Cock #1 - Toggle,
,,Fuel Cock #2 - Prev. Position,
,,Fuel Cock #2 - Next Position,
,,Fuel Cock #2 - Toggle,
,,Fuel Cock #3 - Prev. Position,
,,Fuel Cock #3 - Next Position,
,,Fuel Cock #3 - Toggle,
,,Fuel Cock #4 - Prev. Position,
,,Fuel Cock #4 - Next Position,
,,Fuel Cock #4 - Toggle,
,,Fuel Cock #5 - Prev. Position,
,,Fuel Cock #5 - Next Position,
,,Fuel Cock #5 - Toggle,
,,Fuel Cock #6 - Prev. Position,
,,Fuel Cock #6 - Next Position,
,,Fuel Cock #6 - Toggle,
,,Fuel Cock #7 - Prev. Position,
,,Fuel Cock #7 - Next Position,
,,Fuel Cock #7 - Toggle,
,,Fuel Cock #8 - Prev. Position,
,,Fuel Cock #8 - Next Position,
,,Fuel Cock #8 - Toggle,
,,Toggle Horizontal Stabilizer,
,,Lock Tail Skid,
,,Pitot Heater - Off,
,,Pitot Heater - Toggle,
,,Pitot Heater - On,
,,Stop Motor Compressor,
,,Start Motor Compressor,
,,Close Side Window,
,,Toggle Side Window,
,,Open Side Window,
,,Operate Handpump,
,,Jettison Tanks,
,,Signal Ground Crew to Insert or Remove Chocks,
,,Jump Between Pilot and Selected Position,Control C
,,Select All Engines,Shift Back Quote
,,Deselect All Engines,
,,Select #1 Engine,Shift 1
,,Select #2 Engine,Shift 2
,,Toggle All Engine(s) Selection,
,,Toggle #1 Engine Selection,
,,Toggle #2 Engine Selection,
,,Cylinder Head Temperature Previous Sender,
,,Cylinder Head Temperature Next Sender,
,,Adjust Altimeter - Minus,
,,Adjust Altimeter - Plus,
,,Adjust Anemometer - Minus,
,,Adjust Anemometer - Plus,
,,Directional Gyro - Decrease,
,,Directional Gyro - Increase,
,,Course Setter - Decrease,
,,Course Setter - Increase,
,,Fuel Contents Gauge Selector - Previous,
,,Fuel Contents Gauge Selector - Toggle,
,,Fuel Contents Gauge Selector - Next,
,,Contents Gauge Selector 1 - Previous,
,,Contents Gauge Selector 1 - Toggle,
,,Contents Gauge Selector 1 - Next,
,,Contents Gauge Selector 2 - Previous,
,,Contents Gauge Selector 2 - Toggle,
,,Contents Gauge Selector 2 - Next,
,,Decrease Radio Transceiver Frequency,
,,Increase Radio Transceiver Frequency,
,,Decrease Primary Radio Navigation Frequency,
,,Increase Primary Radio Navigation Frequency,
,,Decrease Secondary Radio Navigation Frequency,
,,Increase Secondary Radio Navigation Frequency,
,,Loop Antenna Left,
,,Loop Antenna Right,
,,Mouse Control,Mouse Left
,Turret,Toggle Park Position,Control O
,,Cruise Position,
,,Firing Position,
,,Move Mount Up,Up
,,Move Mount Down,Down
,,Move Mount Left,Left
,,Move Mount Right,Right
,,Move Gun Up,Shift Up
,,Move Gun Down,Shift Down
,,Move Gun Left,Shift Left
,,Move Gun Right,Shift Right
,View,Manned Position #1,Alt 1
,,Manned Position #2,Alt 2
,,Manned Position #3,Alt 3
,,Manned Position #4,Alt 4
,,Manned Position #5,Alt 5
,,Manned Position #6,Alt 6
,,Manned Position #7,Alt 7
,,Manned Position #8,Alt 8
,,Change Manned Position,Shift C
,,Take Control of Selected AI Aircraft,Alt F1
,,Inside View,F1
,,Glance At Dashboard,
,,Next Manned Position,
,,Change Manned Position,C
,,Release Position,Alt F2
,,External View,F2
,,External Padlock - Enemy Aircraft,F6
,,External View - Nearest Enemy Aircraft,Alt F6
,,External View - Nearest Enemy Vehicle,Alt F7
,,External Padlock - Enemy Vehicle,F7
,,External Flyby,F3
,,External View - Follow,F8
,,External Padlock - Friendly Aircraft,Shift F6
,,External Padlock - Friendly Vehicle,Shift F7
,,External View - Cycle Friendly Targets,Control F2
,,External View - Cycle Armament,
,,External View - Cycle Enemy Targets,
,,External View - Cycle Enemy Vehicles,
,,External View - Cycle Enemy Aircraft,
,,External View - Cycle Enemy Ships,
,,External View - Cycle Friendly Vehicles,
,,External View - Cycle Friendly Aircraft,Shift F2
,,External View - Cycle Friendly Ships,
,,Padlock View,F4
,,Padlock View - Forward,
,,External Padlock - Friendly Aircraft,Shift F4
,,External Padlock - Friendly Vehicle,Shift F5
,,Padlock View - Vehicle,F5
,,Padlock View - Next,U
,,Padlock View - Previous,Y
,,Previous Manned Position,Control C
,,Lean to Gunsight,Shift F1
,,Toggle Cockpit,Control F1
,,Toggle Mirror,M
,,Raise Seat,
,,Zoom In,
,,Zoom Out,
,,Zoom 10°,
,,Zoom 30°,
,,Zoom 50°,
,,Zoom 70° (initial),
,,Zoom 90°,
,,Zoom 110°,
,,Zoom 125°,
,Camera,Interact with Interface Overlay,Alt Mouse Left
,,External Camera Zoom In,
,,External Camera Zoom Out,
,,External Camera Zoom 10°,
,,External Camera Zoom 30°,
,,External Camera Zoom 50°,
,,External Camera Zoom 70° (initial),
,,External Camera Zoom 90°,
,,External Camera Zoom 110°,
,,External Camera Zoom 125°,
,,VR IPD Offset -,
,,VR IPD Offset +,
,,Hold to Adjust Field of View,Mouse 5
,,Hold to Zoom Camera,Mouse Right
,,Hold to Offset Camera,Mouse Left
,,VR Horizontal HUD Position -,
,,VR Horizontal HUD Position +,
,,VR Vertical HUD Position -,
,,VR Vertical HUD Position +,
,,VR HUD Distance -,
,,VR HUD Distance +,
,,VR Minimap Tilt -,
,,VR Minimap Tilt +,
,,Reset View,Numpad/
,,Camera Control in Independent Mode,Mouse Middle
,,Toggle Independent Mode,F10
,,VR Toggle Rotation Amplification,
,,Toggle TrackIR,
,,VR Rotation Amplification -,
,,VR Rotation Amplification +,
,Pan View,Pan Down,NumPad-2
,,Pan Down,Pov 180
,,Pan Left-Up,NumPad-7
,,Pan Left-Up,Pov 315
,,Pan Left,NumPad-4
,,Pan Left,Pov 270
,,Pan Left-Down,NumPad-1
,,Pan Left-Down,Pov 225
,,Return to Front View,NumPad-5
,,Pan Right-Up,NumPad-9
,,Pan Right-Up,Pov 45
,,Pan Right,NumPad-6
,,Pan Right,Pov 90
,,Pan Right-Down,NumPad-3
,,Pan Right-Down,Pov 135
,,Pan Up,NumPad-8
,,Pan Up,Pov 0
,Quick Views,Snap Forward,NumPad-8
,,Snap 45° Up,
,,Snap 90° Up,NumPad-5
,,Snap 90° Up,Pov 0
,,Snap 45° Down,Pov 180
,,Snap 90° Down,NumPad-2
,,Snap 45° Right,NumPad-9
,,Snap 45° Right,Pov 45
,,Snap 45° Right 45° Up,
,,Snap 45° Right 45° Down,
,,Snap 90° Right,Pov 90
,,Snap 90° Right,NumPad-6
,,Snap 90° Right 90° Up,
,,Snap 90° Right 90° Down,
,,Snap 135° Right,Pov 135
,,Snap 135° Right,NumPad-3
,,Snap 135° Right 45° Up,
,,Snap 135° Right 45° Down,
,,Snap 45° Left,Pov 135
,,Snap 45° Left,NumPad-3
,,Snap 45° Left 45° Up,
,,Snap 45° Left 45° Down,
,,Snap 90° Left,Pov 270
,,Snap 90° Left,NumPad-4
,,Snap 90° Left 90° Up,
,,Snap 90° Left 90° Down,
,,Snap 135° Left,Pov 225
,,Snap 135° Left,NumPad-1
,,Snap 135° Left 45° Up,
,,Snap 135° Left 45° Down,
,,Quick Zoom 10°,
,,Quick Zoom 30°,
,,Quick Zoom 50°,
,,Quick Zoom 70° (initial),
,,Quick Zoom 90°,
,,Quick Zoom 110°,
,,Quick Zoom 125°,
,In-Game Time,Accelerate In-Game Time,Close Bracket
,,Decelerate In-Game Time,Open Bracket
,,Reset In-Game Time to Normal,Quote
,Comms,Open Orders Panel,Tab
,,Backspace,Backspace
,,Close Orders Panel,Escape
,,Enter,Enter
,,Expand Selected Order Panel,Control
,,Issue Order #0,0
,,Issue Order #1,1
,,Issue Order #2,2
,,Issue Order #3,3
,,Issue Order #4,4
,,Issue Order #5,5
,,Issue Order #6,6
,,Issue Order #7,7
,,Issue Order #8,8
,,Issue Order #9,9
,,Select Order,Alt
,Chat,Cycle Recipients,
,,Send Message,
,,Flash Net Stats,
,,Toggle All Windows,
,,Toggle Icon Labels,
,,Toggle Net Stats,
,Builder,Add Branch,Control B
,,Delete Waypoint,Alt Left
,,Battle Area,A Mouse Left
,,Break,
,,Previous Item In Category,PageUp
,,First Item in Category,Home
,,Change,PageDown
,,Change +,End
,,Copy,Control C
,,Create Roads,
,,Cursor,Backspace
,,Cursor,Tab
,,Cut,Control X
,,Delete,
,,Delete,NumPad.
,,Delete,Delete
,,Delete,Control Mouse Right
,,Add Waypoint,Alt Right
,,Free View,Enter
,,Insert,
,,Insert,Insert
,,Insert,Control Mouse Left
,,Insert,NumPad-0
,,Toggle Landscape,F10
,,Load Mission,
,,Normal Land,
,,Move Object,Mouse Left
,,Paste,Control V
,,Play Mission,
,,Pop-up Menu,Mouse Right
,,Redo,Control Y
,,Reset Object Orientation,NumPad-5
,,Reset Angle 90,NumPad-8
,,Remove from Group,Alt Mouse Right
,,Select into Group,Alt Mouse Left
,,Show Distance,
,,Show Insert,
,,Rotate Object 1° Right,
,,Rotate Object 1° Left,
,,Rotate Object 15° Right,NumPad-6
,,Rotate Object 15° Left,NumPad-4
,,Rotate Object 30° Right,NumPad-3
,,Rotate Object 30° Left,NumPad-1
,,Rotate Object 5° Right,NumPad-9
,,Rotate Object 5° Left,NumPad-7
,,Straight Spline,
,,Align to Land,
,,Undo,Control Z
,,Deselect,Alt Control
,,World Zoom,Shift Mouse Left
,Vehicle,Fire Current Weapon,Space
,,Accelerate,Up
,,Brake,Down
,,Turn Left,Left
,,Turn Right,Right
,,Stop Movement,
,,Start Movement,
,,Toggle Direction,Alt D
,,Toggle Handbrake,
Axes,Aircraft,Aileron,
,,Elevator,
,,Rudder,
,,Aileron Trim,
,,Elevator Trim,
,,Rudder Trim,
,,Landing Flaps,
,,Leading-Edge Slats,
,,Undercarriage,
,,Wheel Brakes,
,,Left Wheel Brakes,
,,Right Wheel Brakes,
,,Airbrakes,
,,Bomb Bay Doors,
,,Impeller,
,,Throttle,
,,Throttle War Emergency Power,
,,Propeller Pitch,
,,Mixture,
,,Radiator,
,,Oil Radiator,
,,Supercharger,
,,Carburettor Heat,
,,Throttle #1,
,,Throttle #2,
,,Propeller Pitch #1,
,,Propeller Pitch #2,
,,Mixture #1,
,,Mixture #2,
,,Radiator #1,
,,Radiator #2,
,,Oil Radiator #1,
,,Oil Radiator #2,
,,Supercharger #1,
,,Supercharger #2,
,,Carburettor Heat #1,
,,Carburettor Heat #2,
,,Fuel Cock #1,
,,Fuel Cock #2,
,,Fuel Cock #3,
,,Fuel Cock #4,
,Turret,Yaw,
,,Pitch,
,Vehicle,Power,
""";

    // ── Parse ─────────────────────────────────────────────────────────────────

    private static (List<ControlCategory> Keys, List<ControlCategory> Axes) _parsed = ParseCsv();

    public static List<ControlCategory> KeyCategories  => _parsed.Keys;
    public static List<ControlCategory> AxisCategories => _parsed.Axes;

    private static (List<ControlCategory> Keys, List<ControlCategory> Axes) ParseCsv()
    {
        var keyCats  = new List<ControlCategory>();
        var axisCats = new List<ControlCategory>();

        string currentType = "Keys";
        List<ControlEntry>? currentEntries = null;
        string lastAction = "";
        string currentCategoryName = "";

        foreach (var rawLine in Csv.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("CONTROLS")) continue;

            // Split on commas (we only have 4 columns; action may contain commas –
            // so split into exactly 4 parts, letting the 3rd part take extras).
            var parts = line.Split(',');
            if (parts.Length < 3) continue;

            string col0  = parts[0].Trim(); // CONTROLS type (Keys/Axes) or ""
            string col1  = parts[1].Trim(); // Category or ""
            string action = parts[2].Trim(); // Action name or ""
            string bind  = parts.Length >= 4 ? parts[3].Trim() : "";

            // Update control type (Keys / Axes)
            if (!string.IsNullOrEmpty(col0))
                currentType = col0;

            // New category
            if (!string.IsNullOrEmpty(col1))
            {
                var cat = new ControlCategory { Name = col1, IsAxis = currentType == "Axes" };
                currentEntries = cat.Entries;
                currentCategoryName = col1;
                if (currentType == "Axes") axisCats.Add(cat);
                else                        keyCats.Add(cat);
                lastAction = "";
            }

            if (currentEntries == null) continue;
            if (string.IsNullOrEmpty(action)) continue;

            // Skip separator lines
            if (action.StartsWith("---")) { lastAction = ""; continue; }

            // Group multiple bindings for the same consecutive action
            if (action == lastAction && currentEntries.Count > 0)
            {
                var prev = currentEntries[^1];
                if (!string.IsNullOrEmpty(bind))
                    prev.Bindings.Add(NormalizeBinding(bind));
            }
            else
            {
                var entry = new ControlEntry
                {
                    Action         = action,
                    Bindings       = string.IsNullOrEmpty(bind) ? [] : [NormalizeBinding(bind)],
                    IsAxis         = currentType == "Axes",
                    SourceCategory = currentCategoryName
                };
                currentEntries.Add(entry);
                lastAction = action;
            }
        }

        return (Normalize(keyCats, false), Normalize(axisCats, true));
    }

    private static List<ControlCategory> Normalize(List<ControlCategory> rawCategories, bool isAxis)
    {
        var buckets = new Dictionary<string, List<ControlEntry>>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in rawCategories.SelectMany(c => c.Entries))
        {
            var category = isAxis ? "Flight Axes" : DisplayCategory(entry.Action, entry.SourceCategory);
            var normalized = new ControlEntry
            {
                Action      = NormalizeActionName(entry.Action),
                Bindings    = entry.Bindings,
                IsSeparator = entry.IsSeparator,
                IsAxis      = entry.IsAxis,
                SourceCategory = entry.SourceCategory,
                Group       = DisplayGroup(entry.Action),
                IsEssential = IsEssential(entry.Action, isAxis),
                EngineMode  = EngineMode(entry.Action),
                PreviewPercent = AxisPreview(entry.Action),
                IsIncremental = IsIncremental(entry.Action)
            };

            if (!buckets.TryGetValue(category, out var list))
            {
                list = [];
                buckets[category] = list;
            }
            list.Add(normalized);
        }

        return buckets
            .OrderBy(kvp => CategoryOrder(kvp.Key))
            .ThenBy(kvp => kvp.Key)
            .Select(kvp =>
            {
                var entries = kvp.Value
                    .OrderBy(e => GroupOrder(e.Group))
                    .ThenBy(e => e.Group)
                    .ThenBy(e => NaturalSortKey(e.Action))
                    .ToList();
                var groups = entries
                    .GroupBy(e => e.Group)
                    .Select(g => new ControlGroup { Name = g.Key, Entries = g.ToList() })
                    .ToList();
                return new ControlCategory
                {
                    Name = kvp.Key,
                    Entries = entries,
                    Groups = groups,
                    IsAxis = isAxis
                };
            })
            .ToList();
    }

    private static int CategoryOrder(string category) => category switch
    {
        "Flight" => 0,
        "Engine" => 1,
        "Weapons" => 2,
        "View" => 3,
        "Cockpit" => 4,
        "Commands" => 5,
        "Game" => 6,
        "Flight Axes" => 0,
        _ => 99
    };

    public static int GroupSortKey(string group) => GroupOrder(group);

    private static int GroupOrder(string group) => group switch
    {
        "General" => 0,
        "Primary Flight" => 1,
        "Engine Selection" => 1,
        "Throttle" => 2,
        "Propeller Pitch" => 3,
        "Mixture" => 4,
        "Air Filter" => 5,
        "Radiators" => 6,
        "Mixture Increments" => 7,
        "Throttle Increments" => 8,
        "Propeller Pitch Increments" => 9,
        "Brakes" => 9,
        "Landing Gear" => 10,
        "Flaps" => 11,
        "Trim" => 12,
        "Guns" => 13,
        "Bombs" => 14,
        "Sights" => 15,
        "Turrets" => 16,
        "Canopy" => 17,
        "Jettison" => 18,
        "Illumination" => 19,
        "Radio" => 20,
        "Gauge" => 21,
        "Order Panel" => 22,
        "Orders" => 23,
        "VR" => 24,
        "Quick Views" => 25,
        "Snap Views" => 26,
        "Pan" => 27,
        "External Views" => 28,
        "Padlock" => 29,
        _ => 99
    };

    private static string DisplayCategory(string action, string sourceCategory)
    {
        var a = action.ToLowerInvariant();
        if (ContainsAny(a, "air filter", "war emergency", "slow running cut-out"))
            return "Engine";
        if (ContainsAny(a, "side window", "contents gauge selector", "illumination"))
            return "Cockpit";
        if (ContainsAny(a, "orders panel", "issue order", "select order", "expand selected order")
            || action is "Enter" or "Backspace")
            return "Commands";
        if (sourceCategory == "Pan View")
            return "View";
        if (ContainsAny(a, "jettison canopy", "jettison tanks"))
            return "Flight";
        if (sourceCategory == "Turret")
            return "Weapons";
        if (ContainsAny(a, "throttle", "propeller", "mixture", "radiator", "supercharger", "carburettor", "magneto", "engine", "fuel cock", "impeller", "boost", "compressor"))
            return "Engine";
        if (ContainsAny(a, "fire guns", "bomb", "sight", "gunpod", "weapon", "ordnance"))
            return "Weapons";
        if (ContainsAny(a, "radio", "canopy", "hatch", "door", "illumination", "stopwatch", "clock", "voltamperemeter", "pitot", "altimeter", "anemometer", "directional gyro", "course setter", "fuel contents"))
            return "Cockpit";
        if (ContainsAny(a, "brake", "undercarriage", "flap", "slat", "trim", "aileron", "elevator", "rudder", "airbrake", "horizontal stabilizer", "tail skid"))
            return "Flight";
        if (ContainsAny(a, "view", "camera", "zoom", "padlock", "snap", "trackir", "vr", "manned position", "cockpit", "mirror"))
            return "View";
        if (ContainsAny(a, "orders", "comms", "enter", "backspace"))
            return "Commands";
        return sourceCategory is "Game" or "Builder" or "Vehicle" ? sourceCategory : "Game";
    }

    private static string DisplayGroup(string action)
    {
        var a = action.ToLowerInvariant();
        if (ContainsAny(a, "air filter")) return "Air Filter";
        if (ContainsAny(a, "war emergency", "slow running cut-out")) return "Throttle";
        if (ContainsAny(a, "side window")) return "Side Window";
        if (ContainsAny(a, "contents gauge selector")) return "Gauge";
        if (ContainsAny(a, "vr ")) return "VR";
        if (ContainsAny(a, "quick")) return "Quick Views";
        if (ContainsAny(a, "snap")) return "Snap Views";
        if (ContainsAny(a, "pan ")) return "Pan";
        if (ContainsAny(a, "external")) return "External Views";
        if (ContainsAny(a, "padlock")) return "Padlock";
        if (ContainsAny(a, "jettison")) return "Jettison";
        if (ContainsAny(a, "turret")) return "Turrets";
        if (ContainsAny(a, "radio")) return "Radio";
        if (ContainsAny(a, "orders panel")) return "Order Panel";
        if (ContainsAny(a, "issue order", "select order", "expand selected order", "enter", "backspace")) return "Orders";
        if (ContainsAny(a, "brake")) return "Brakes";
        if (ContainsAny(a, "undercarriage", "tail skid", "chocks")) return "Landing Gear";
        if (ContainsAny(a, "flap", "slat")) return "Flaps";
        if (ContainsAny(a, "trim")) return "Trim";
        if (ContainsAny(a, "aileron", "elevator", "rudder", "stabilizer")) return "Primary Flight";
        if (IsPercentIncrement(a, "mixture")) return "Mixture Increments";
        if (IsPercentIncrement(a, "throttle")) return "Throttle Increments";
        if (IsPercentIncrement(a, "propeller pitch")) return "Propeller Pitch Increments";
        if (ContainsAny(a, "throttle", "boost", "slow running")) return "Throttle";
        if (ContainsAny(a, "propeller", "feather")) return "Propeller Pitch";
        if (ContainsAny(a, "mixture")) return "Mixture";
        if (ContainsAny(a, "radiator", "oil radiator")) return "Radiators";
        if (ContainsAny(a, "supercharger", "carburettor")) return "Induction";
        if (ContainsAny(a, "magneto", "engine selection", "select #", "select all engines", "engine")) return "Engine Selection";
        if (ContainsAny(a, "fuel cock")) return "Fuel System";
        if (ContainsAny(a, "canopy", "hatch", "door")) return "Canopy";
        if (ContainsAny(a, "illumination", "light")) return "Illumination";
        if (ContainsAny(a, "bomb")) return "Bombs";
        if (ContainsAny(a, "sight")) return "Sights";
        if (ContainsAny(a, "gun", "weapon", "ordnance")) return "Guns";
        if (ContainsAny(a, "zoom", "camera", "view", "padlock", "snap")) return "View";
        return "General";
    }

    private static string NormalizeActionName(string action)
    {
        var replacements = new Dictionary<string, string>
        {
            ["Toggle Canopy / Hatch / Door"] = "Canopy / Hatch / Door - Toggle",
            ["Close Canopy / Hatch / Door"] = "Canopy / Hatch / Door - Close",
            ["Open Canopy / Hatch / Door"] = "Canopy / Hatch / Door - Open",
            ["Jettison Canopy / Hatch / Door"] = "Canopy / Hatch / Door - Jettison",
            ["Decrease Primary Illumination"] = "Primary Cockpit Illumination - Decrease",
            ["Toggle Primary Cockpit Illumination"] = "Primary Cockpit Illumination - Toggle",
            ["Increase Primary Illumination"] = "Primary Cockpit Illumination - Increase",
            ["Decrease Secondary Illumination"] = "Secondary Cockpit Illumination - Decrease",
            ["Toggle Secondary Cockpit Illumination"] = "Secondary Cockpit Illumination - Toggle",
            ["Increase Secondary Illumination"] = "Secondary Cockpit Illumination - Increase",
            ["Start Selected Engines"] = "Engine - Start Selected",
        };
        if (replacements.TryGetValue(action, out var named)) return named;

        var directional = NormalizeDirectionalAction(action);
        if (directional is not null) return directional;

        foreach (var prefix in new[] { "Increase ", "Decrease ", "Toggle ", "Open ", "Close ", "Raise ", "Lower ", "Retract ", "Extend ", "Reset ", "Select Previous ", "Select Next ", "Previous ", "Next " })
        {
            if (action.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return $"{action[prefix.Length..]} - {prefix.Trim()}";
        }
        return action;
    }

    private static string? NormalizeDirectionalAction(string action)
    {
        string[] systems = ["Rudder", "Aileron", "Elevator", "Undercarriage", "Landing Flaps", "Leading-Edge Slats", "Trim Aileron", "Trim Elevator", "Trim Rudder"];
        foreach (var system in systems.OrderByDescending(s => s.Length))
        {
            if (action.StartsWith(system + " ", StringComparison.OrdinalIgnoreCase))
                return $"{system} - {action[(system.Length + 1)..]}";
            if (action.EndsWith(" " + system, StringComparison.OrdinalIgnoreCase))
                return $"{system} - {action[..^(system.Length + 1)]}";
        }

        foreach (var prefix in new[] { "Manually Raise ", "Manually Lower " })
        {
            if (action.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var system = action[prefix.Length..];
                return $"{system} - {prefix.Trim()}";
            }
        }

        if (action.StartsWith("Open Orders Panel", StringComparison.OrdinalIgnoreCase))
            return "Order Panel - Open";
        if (action.StartsWith("Close Orders Panel", StringComparison.OrdinalIgnoreCase))
            return "Order Panel - Close";
        if (action.StartsWith("Expand Selected Order Panel", StringComparison.OrdinalIgnoreCase))
            return "Order Panel - Expand Selected";
        if (action.StartsWith("Issue Order #", StringComparison.OrdinalIgnoreCase))
            return "Order - " + action["Issue ".Length..];
        if (action.Equals("Select Order", StringComparison.OrdinalIgnoreCase))
            return "Order - Select";
        if (action.Equals("Enter", StringComparison.OrdinalIgnoreCase))
            return "Order - Enter";
        if (action.Equals("Backspace", StringComparison.OrdinalIgnoreCase))
            return "Order - Backspace";

        return null;
    }

    private static string NormalizeBinding(string binding)
    {
        var parts = binding.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                           .Select(p => p.Equals("Control", StringComparison.OrdinalIgnoreCase) ? "Ctrl" : p)
                           .ToList();

        var mouseIndex = parts.FindIndex(p => p.Equals("Mouse", StringComparison.OrdinalIgnoreCase));
        if (mouseIndex >= 0 && mouseIndex + 1 < parts.Count)
        {
            string click = parts[mouseIndex + 1].Equals("Left", StringComparison.OrdinalIgnoreCase)
                ? "Left Click"
                : parts[mouseIndex + 1].Equals("Right", StringComparison.OrdinalIgnoreCase)
                    ? "Right Click"
                    : "Mouse " + parts[mouseIndex + 1];
            parts.RemoveRange(mouseIndex, 2);
            parts.Add(click);
        }

        return parts.Count > 1 ? string.Join(" + ", parts) : string.Join("", parts);
    }

    private static bool IsPercentIncrement(string action, string system)
        => action.StartsWith(system, StringComparison.OrdinalIgnoreCase)
           && action.Any(char.IsDigit)
           && action.Contains('%');

    private static bool IsIncremental(string action)
    {
        var a = action.ToLowerInvariant();
        return action.Contains('%')
               || ContainsAny(a, "one notch", "10°", "30°", "50°", "70°", "90°", "110°", "125°");
    }

    private static bool IsEssential(string action, bool isAxis)
    {
        var a = action.ToLowerInvariant();
        return isAxis
            ? ContainsAny(a, "aileron", "elevator", "rudder", "throttle", "wheel brakes")
            : ContainsAny(a, "fire guns", "drop ordnance", "undercarriage", "flaps", "wheel brakes", "map", "inside view", "external view", "pause", "bail out", "start selected engines", "radiator", "trim elevator", "canopy", "aileron", "elevator", "rudder", "throttle", "propeller pitch");
    }

    private static string EngineMode(string action)
    {
        var a = action.ToLowerInvariant();
        if (a.Contains("start selected engines"))
            return "All";
        if (ContainsAny(a, "#1", "#2", "#3", "#4", "#5", "#6", "#7", "#8", "all engines", "engine selection", "selected engines"))
            return "Multi";
        if (ContainsAny(a, "throttle", "propeller", "mixture", "radiator", "magneto", "supercharger", "carburettor", "fuel cock", "engine"))
            return "Single";
        return "All";
    }

    private static int AxisPreview(string action)
    {
        var a = action.ToLowerInvariant();
        if (a.Contains("throttle")) return 72;
        if (a.Contains("brake")) return 28;
        if (a.Contains("trim")) return 47;
        if (a.Contains("radiator")) return 63;
        return 55;
    }

    private static bool ContainsAny(string source, params string[] needles)
        => needles.Any(source.Contains);

    public static string NaturalSortKey(string value)
        => Regex.Replace(value, @"\d+", m => int.Parse(m.Value, CultureInfo.InvariantCulture).ToString("D4", CultureInfo.InvariantCulture));
}

// ── Screen ────────────────────────────────────────────────────────────────────

public partial class ControlsScreen : UserControl
{
    private bool   _showAxes;
    private bool   _simpleMode = true;
    private bool?  _singleEngineMode;
    private bool   _showIncremental;
    private string _selectedCategory = "";
    private string _searchText       = "";
    private readonly List<ControlEntry> _visibleEntries = [];

    // Currently displayed set of categories
    private List<ControlCategory> CurrentCategories
        => _showAxes ? ControlsData.AxisCategories : ControlsData.KeyCategories;

    public ControlsScreen()
    {
        InitializeComponent();
    }

    // ── Loaded ────────────────────────────────────────────────────────────────

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateAxisColumnVisibility();
        RefreshCategoryList();
        SelectFirstCategory();
    }

    // ── Category sidebar ──────────────────────────────────────────────────────

    private void RefreshCategoryList()
    {
        CategoryList.SelectionChanged -= Category_SelectionChanged; // suppress during rebuild
        CategoryList.Items.Clear();
        foreach (var cat in CurrentCategories)
            CategoryList.Items.Add(cat);

        if (CategoryList.Items.Count > 0)
            CategoryList.SelectedIndex = 0;

        _selectedCategory = CurrentCategories.FirstOrDefault()?.Name ?? "";
        CategoryList.SelectionChanged += Category_SelectionChanged;
    }

    private void SelectFirstCategory()
    {
        _selectedCategory = CurrentCategories.FirstOrDefault()?.Name ?? "";
        RefreshActionList();
    }

    private void Category_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (CategoryList.SelectedItem is ControlCategory cat)
        {
            _selectedCategory = cat.Name;
            RefreshActionList();
        }
    }

    // ── Mode toggle (Keys / Axes) ─────────────────────────────────────────────

    private void KeysMode_Click(object sender, RoutedEventArgs e)
    {
        if (_showAxes)
        {
            _showAxes         = false;
            _searchText       = "";
            SearchBox.Text    = "";
            KeysModeBtn.Style = (Style)FindResource("ModeBtnActive");
            AxesModeBtn.Style = (Style)FindResource("ModeBtn");
            UpdateAxisColumnVisibility();
            RefreshCategoryList();
            SelectFirstCategory();
        }
    }

    private void AxesMode_Click(object sender, RoutedEventArgs e)
    {
        if (!_showAxes)
        {
            _showAxes         = true;
            _searchText       = "";
            SearchBox.Text    = "";
            AxesModeBtn.Style = (Style)FindResource("ModeBtnActive");
            KeysModeBtn.Style = (Style)FindResource("ModeBtn");
            UpdateAxisColumnVisibility();
            RefreshCategoryList();
            SelectFirstCategory();
        }
    }

    // ── Search ────────────────────────────────────────────────────────────────

    private void Search_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchText = SearchBox.Text.Trim();
        RefreshActionList();
    }

    private void ClearSearch_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Clear();
        SearchBox.Focus();
    }

    // ── Action list ───────────────────────────────────────────────────────────

    private void RefreshActionList()
    {
        ActionList.Items.Clear();

        // Determine source: when searching, search across all categories
        IEnumerable<ControlEntry> source;
        if (!string.IsNullOrEmpty(_searchText))
        {
            source = CurrentCategories
                .SelectMany(c => c.Entries)
                .Where(e => !e.IsSeparator &&
                            e.Action.Contains(_searchText, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            var cat = CurrentCategories.FirstOrDefault(c => c.Name == _selectedCategory);
            source  = cat?.Entries ?? [];
        }

        source = ApplyFilters(source);

        _visibleEntries.Clear();
        _visibleEntries.AddRange(source);

        foreach (var group in _visibleEntries.GroupBy(e => e.Group)
                     .OrderBy(g => ControlsData.GroupSortKey(g.Key))
                     .ThenBy(g => g.Key)
                     .Select(g => new ControlGroup
                     {
                         Name = g.Key,
                         Entries = g.OrderBy(e => ControlsData.NaturalSortKey(e.Action)).ToList()
                     }))
        {
            ActionList.Items.Add(group);
        }
    }

    private IEnumerable<ControlEntry> ApplyFilters(IEnumerable<ControlEntry> source)
    {
        if (_simpleMode)
            source = source.Where(e => e.IsEssential);

        if (!_showIncremental)
            source = source.Where(e => !e.IsIncremental);

        if (_singleEngineMode is null)
            return source;

        string desired = _singleEngineMode.Value ? "Single" : "Multi";
        return source.Where(e => e.EngineMode == "All" || e.EngineMode == desired);
    }

    private void SimpleMode_Click(object sender, RoutedEventArgs e)
    {
        _simpleMode = true;
        SimpleModeBtn.Style = (Style)FindResource("ModeBtnActive");
        AdvancedModeBtn.Style = (Style)FindResource("ModeBtn");
        RefreshActionList();
    }

    private void AdvancedMode_Click(object sender, RoutedEventArgs e)
    {
        _simpleMode = false;
        AdvancedModeBtn.Style = (Style)FindResource("ModeBtnActive");
        SimpleModeBtn.Style = (Style)FindResource("ModeBtn");
        RefreshActionList();
    }

    private void SingleEngine_Click(object sender, RoutedEventArgs e)
    {
        _singleEngineMode = _singleEngineMode == true ? null : true;
        RefreshEngineModeButtons();
        RefreshActionList();
    }

    private void MultiEngine_Click(object sender, RoutedEventArgs e)
    {
        _singleEngineMode = _singleEngineMode == false ? null : false;
        RefreshEngineModeButtons();
        RefreshActionList();
    }

    private void Incremental_Click(object sender, RoutedEventArgs e)
    {
        _showIncremental = !_showIncremental;
        IncrementalBtn.Style = (Style)FindResource(_showIncremental ? "ModeBtnActive" : "ModeBtn");
        RefreshActionList();
    }

    private void RefreshEngineModeButtons()
    {
        SingleEngineBtn.Style = (Style)FindResource(_singleEngineMode == true ? "ModeBtnActive" : "ModeBtn");
        MultiEngineBtn.Style = (Style)FindResource(_singleEngineMode == false ? "ModeBtnActive" : "ModeBtn");
    }

    private void UpdateAxisColumnVisibility()
    {
        if (AxisHeader is null || AxisHeaderColumn is null) return;
        AxisHeader.Visibility = _showAxes ? Visibility.Visible : Visibility.Collapsed;
        AxisHeaderColumn.Width = _showAxes ? new GridLength(1.3, GridUnitType.Star) : new GridLength(0);
        var altVisibility = _showAxes ? Visibility.Collapsed : Visibility.Visible;
        var altWidth = _showAxes ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        BindingAlt1Header.Visibility = altVisibility;
        BindingAlt2Header.Visibility = altVisibility;
        BindingAlt3Header.Visibility = altVisibility;
        BindingAlt1HeaderColumn.Width = altWidth;
        BindingAlt2HeaderColumn.Width = altWidth;
        BindingAlt3HeaderColumn.Width = altWidth;
    }

    private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt)
            return;

        var binding = FormatPressedKey(e);
        foreach (var entry in _visibleEntries)
            entry.SetActiveBinding(entry.Bindings.Contains(binding) ? binding : "");
    }

    private void UserControl_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        foreach (var entry in _visibleEntries)
            entry.SetActiveBinding("");
    }

    private static string FormatPressedKey(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var parts = new List<string>();
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) parts.Add("Ctrl");
        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) parts.Add("Shift");
        if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
        parts.Add(KeyToDisplay(key));
        return string.Join(" + ", parts);
    }

    private static string KeyToDisplay(Key key)
    {
        var s = key.ToString();
        if (s.StartsWith("D") && s.Length == 2 && char.IsDigit(s[1])) return s[1].ToString();
        return key switch
        {
            Key.OemMinus => "Minus",
            Key.OemPlus => "Equals",
            Key.OemOpenBrackets => "Open Bracket",
            Key.OemCloseBrackets => "Close Bracket",
            Key.OemQuotes => "Quote",
            Key.PrintScreen => "Print Screen",
            _ => s
        };
    }

    // ── Footer buttons ────────────────────────────────────────────────────────

    private void Back_Click(object sender, RoutedEventArgs e)
        => NavigationService.Back();

    private void Apply_Click(object sender, RoutedEventArgs e)
        => NavigationService.Back();

    private void Load_Click(object sender, RoutedEventArgs e)
        => MessageBox.Show("Load preset not yet implemented.", "LOAD",
                           MessageBoxButton.OK, MessageBoxImage.Information);

    private void SaveAs_Click(object sender, RoutedEventArgs e)
        => MessageBox.Show("Save preset not yet implemented.", "SAVE AS",
                           MessageBoxButton.OK, MessageBoxImage.Information);
}
