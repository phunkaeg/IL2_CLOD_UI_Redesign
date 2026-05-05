namespace PlaneLoadoutWpfTest;

/// <summary>
/// Persisted user preferences — mission folder paths and UI window settings.
/// Serialised to %AppData%\FunkUI\settings.json via AppSettingsService.
/// </summary>
public class AppSettings
{
    // ── UI window ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Desired window width in device-independent pixels.
    /// 0 = start maximised (default).
    /// </summary>
    public int WindowWidth  { get; set; } = 0;

    /// <summary>
    /// Desired window height in device-independent pixels.
    /// 0 = start maximised (default).
    /// </summary>
    public int WindowHeight { get; set; } = 0;

    /// <summary>
    /// Main menu logo glow animation: "Fire" or "Breathe".
    /// </summary>
    public string LogoGlowMode { get; set; } = "Fire";

    // ── Mission folders ───────────────────────────────────────────────────────

    /// <summary>
    /// Cliffs of Dover install folder. Derived scanners look under parts\bob,
    /// parts\tobruk, missions\Single and missions\Multi.
    /// </summary>
    public string GameRootPath { get; set; } = string.Empty;

    /// <summary>
    /// Documents root. Usually the user's Documents or OneDrive Documents folder.
    /// Derived scanners append the 1C SoftClub Cliffs of Dover folder.
    /// </summary>
    public string DocumentsRootPath { get; set; } = string.Empty;

    /// <summary>
    /// Legacy path retained so older saved settings still deserialize.
    /// </summary>
    public string BoBSingleMissionsPath    { get; set; } = string.Empty;

    /// <summary>
    /// Legacy path retained so older saved settings still deserialize.
    /// </summary>
    public string TobrukSingleMissionsPath { get; set; } = string.Empty;

    /// <summary>
    /// Legacy path retained so older saved settings still deserialize.
    /// </summary>
    public string QuickMissionsPath        { get; set; } = string.Empty;

    /// <summary>
    /// Legacy path retained so older saved settings still deserialize.
    /// </summary>
    public string LennyCampaignsPath       { get; set; } = string.Empty;

    /// <summary>
    /// Legacy path retained so older saved settings still deserialize.
    /// </summary>
    public string UserMissionsPath         { get; set; } = string.Empty;
}
