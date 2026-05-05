namespace PlaneLoadoutWpfTest.Services;

/// <summary>
/// Static data service providing aircraft, squadron, and weather-preset lists.
/// All methods currently return hardcoded data.  Swap the method bodies for
/// engine-bridge calls when the IL-2 CloD API is available — no XAML or
/// screen code needs to change.
/// </summary>
public static class DataService
{
    // ── Domain records ────────────────────────────────────────────────────────

    public enum AircraftRole { Fighter, Bomber, Attacker, Trainer }

    public sealed record Aircraft(
        string Name,
        string Side,        // "Allies" | "Axis"
        string Country,
        AircraftRole Role);

    public sealed record Squadron(string Name, string Country);

    /// <param name="CloudBaseKft">
    ///   Suggested cloud base in thousands of feet, or null if no clouds
    ///   (Clear / Haze presets).
    /// </param>
    public sealed record WeatherPreset(
        string Name,
        string Description,
        int? CloudBaseKft = null);

    // ── Aircraft list (full CloD / Desert Wings Tobruk roster) ───────────────

    private static readonly Aircraft[] s_aircraft =
    [
        // ── Allies — United Kingdom ──────────────────────────────────────────
        new("Bristol Beaufighter Mk IF",              "Allies", "United Kingdom", AircraftRole.Fighter),
        new("Bristol Beaufighter Mk IF Late / Mk IC", "Allies", "United Kingdom", AircraftRole.Fighter),
        new("Bristol Blenheim Mk IV",                 "Allies", "United Kingdom", AircraftRole.Bomber),
        new("De Havilland DH.82 Tiger Moth",          "Allies", "United Kingdom", AircraftRole.Trainer),
        new("Gloster Gladiator Mk II",                "Allies", "United Kingdom", AircraftRole.Fighter),
        new("Hawker Hurricane Mk I",                  "Allies", "United Kingdom", AircraftRole.Fighter),
        new("Hawker Hurricane Mk II",                 "Allies", "United Kingdom", AircraftRole.Fighter),
        new("Martlet Mk III",                         "Allies", "United Kingdom", AircraftRole.Fighter),
        new("Supermarine Spitfire Mk Ia",             "Allies", "United Kingdom", AircraftRole.Fighter),
        new("Supermarine Spitfire Mk IIa",            "Allies", "United Kingdom", AircraftRole.Fighter),
        new("Supermarine Spitfire Mk IIb",            "Allies", "United Kingdom", AircraftRole.Fighter),
        new("Supermarine Spitfire Mk V (Non HF)",     "Allies", "United Kingdom", AircraftRole.Fighter),
        new("Supermarine Spitfire Mk V HF",           "Allies", "United Kingdom", AircraftRole.Fighter),
        new("Vickers Wellington Mk I",                "Allies", "United Kingdom", AircraftRole.Bomber),
        // ── Allies — United States ───────────────────────────────────────────
        new("Kittyhawk Mk Ia",  "Allies", "United States", AircraftRole.Fighter),
        new("Tomahawk Mk II",   "Allies", "United States", AircraftRole.Fighter),
        // ── Allies — France ──────────────────────────────────────────────────
        new("Dewoitine D.520",  "Allies", "France", AircraftRole.Fighter),
        // ── Axis — Germany ───────────────────────────────────────────────────
        new("Heinkel He 111 (P-2, H-2, H-6)",        "Axis", "Germany", AircraftRole.Bomber),
        new("Junkers Ju 87 B-2",                      "Axis", "Germany", AircraftRole.Attacker),
        new("Junkers Ju 88 (A-1, A-5, C-1, C-2, C4)","Axis", "Germany", AircraftRole.Bomber),
        new("Messerschmitt Bf 108 Taifun",            "Axis", "Germany", AircraftRole.Trainer),
        new("Messerschmitt Bf 109 E-1 / E-3",         "Axis", "Germany", AircraftRole.Fighter),
        new("Messerschmitt Bf 109 E-4 / E-4B",        "Axis", "Germany", AircraftRole.Fighter),
        new("Messerschmitt Bf 109 E-4N / E-7N",       "Axis", "Germany", AircraftRole.Fighter),
        new("Messerschmitt Bf 109 E-7",               "Axis", "Germany", AircraftRole.Fighter),
        new("Messerschmitt Bf 109 F-1 / F-2",         "Axis", "Germany", AircraftRole.Fighter),
        new("Messerschmitt Bf 109 F-4",               "Axis", "Germany", AircraftRole.Fighter),
        new("Messerschmitt Bf 110 C-4, C-4B",         "Axis", "Germany", AircraftRole.Fighter),
        new("Messerschmitt Bf 110 C-4N, C-6, C-7",    "Axis", "Germany", AircraftRole.Fighter),
        // ── Axis — Italy ─────────────────────────────────────────────────────
        new("Fiat BR.20M Cicogna",  "Axis", "Italy", AircraftRole.Bomber),
        new("Fiat CR.42 Falco",     "Axis", "Italy", AircraftRole.Fighter),
        new("Fiat G.50 Freccia",    "Axis", "Italy", AircraftRole.Fighter),
        new("Macchi C.202 Folgore", "Axis", "Italy", AircraftRole.Fighter),
    ];

    public static IReadOnlyList<Aircraft> GetAllAircraft()                => s_aircraft;
    public static IEnumerable<Aircraft>   GetAircraftBySide(string side)  => s_aircraft.Where(a => a.Side == side);
    public static IEnumerable<Aircraft>   GetAircraftByCountry(string c)  => s_aircraft.Where(a => a.Country == c);

    // ── Countries per Army (Red = Allies / Blue = Axis) ──────────────────────

    public static string[] GetCountriesForArmy(string army) => army switch
    {
        "Red"  => ["United Kingdom", "United States", "France"],
        "Blue" => ["Germany", "Italy"],
        _      => []
    };

    // ── Squadron list (historically accurate BOB / Desert Wings units) ────────

    private static readonly Dictionary<string, Squadron[]> s_squadrons = new()
    {
        ["United Kingdom"] =
        [
            new("No. 1 Sqn RAF",              "United Kingdom"),
            new("No. 17 Sqn RAF",             "United Kingdom"),
            new("No. 19 Sqn RAF",             "United Kingdom"),
            new("No. 23 Sqn RAF",             "United Kingdom"),
            new("No. 25 Sqn RAF",             "United Kingdom"),
            new("No. 29 Sqn RAF",             "United Kingdom"),
            new("No. 32 Sqn RAF",             "United Kingdom"),
            new("No. 43 Sqn RAF",             "United Kingdom"),
            new("No. 46 Sqn RAF",             "United Kingdom"),
            new("No. 54 Sqn RAF",             "United Kingdom"),
            new("No. 56 Sqn RAF",             "United Kingdom"),
            new("No. 64 Sqn RAF",             "United Kingdom"),
            new("No. 65 Sqn RAF",             "United Kingdom"),
            new("No. 66 Sqn RAF",             "United Kingdom"),
            new("No. 72 Sqn RAF",             "United Kingdom"),
            new("No. 73 Sqn RAF",             "United Kingdom"),
            new("No. 74 Sqn RAF",             "United Kingdom"),
            new("No. 79 Sqn RAF",             "United Kingdom"),
            new("No. 85 Sqn RAF",             "United Kingdom"),
            new("No. 87 Sqn RAF",             "United Kingdom"),
            new("No. 92 Sqn RAF",             "United Kingdom"),
            new("No. 111 Sqn RAF",            "United Kingdom"),
            new("No. 145 Sqn RAF",            "United Kingdom"),
            new("No. 151 Sqn RAF",            "United Kingdom"),
            new("No. 213 Sqn RAF",            "United Kingdom"),
            new("No. 222 Sqn RAF",            "United Kingdom"),
            new("No. 229 Sqn RAF",            "United Kingdom"),
            new("No. 234 Sqn RAF",            "United Kingdom"),
            new("No. 238 Sqn RAF",            "United Kingdom"),
            new("No. 242 Sqn RAF (RCAF)",     "United Kingdom"),
            new("No. 249 Sqn RAF",            "United Kingdom"),
            new("No. 253 Sqn RAF",            "United Kingdom"),
            new("No. 257 Sqn RAF",            "United Kingdom"),
            new("No. 303 Sqn RAF (Polish)",   "United Kingdom"),
            new("No. 310 Sqn RAF (Czech)",    "United Kingdom"),
            new("No. 501 Sqn RAF",            "United Kingdom"),
            new("No. 601 Sqn RAuxAF",         "United Kingdom"),
            new("No. 602 Sqn RAuxAF",         "United Kingdom"),
            new("No. 603 Sqn RAuxAF",         "United Kingdom"),
            new("No. 605 Sqn RAuxAF",         "United Kingdom"),
            new("No. 609 Sqn RAuxAF",         "United Kingdom"),
            new("No. 610 Sqn RAuxAF",         "United Kingdom"),
            new("No. 611 Sqn RAuxAF",         "United Kingdom"),
            new("No. 616 Sqn RAuxAF",         "United Kingdom"),
        ],
        ["United States"] =
        [
            new("23rd Fighter Group",         "United States"),
            new("57th Fighter Group",         "United States"),
            new("79th Fighter Group",         "United States"),
        ],
        ["France"] =
        [
            new("GC I/1",    "France"),
            new("GC II/1",   "France"),
            new("GC III/1",  "France"),
            new("GC I/3",    "France"),
            new("GC II/3",   "France"),
            new("GC III/3",  "France"),
            new("GC II/5",   "France"),
            new("GC III/6",  "France"),
        ],
        ["Germany"] =
        [
            new("JG 2 Richthofen",   "Germany"),
            new("JG 3 Udet",         "Germany"),
            new("JG 26 Schlageter",  "Germany"),
            new("JG 27",             "Germany"),
            new("JG 51",             "Germany"),
            new("JG 52",             "Germany"),
            new("JG 53 Pik-As",      "Germany"),
            new("JG 54",             "Germany"),
            new("JG 77",             "Germany"),
            new("KG 1",              "Germany"),
            new("KG 2",              "Germany"),
            new("KG 3",              "Germany"),
            new("KG 4",              "Germany"),
            new("KG 26",             "Germany"),
            new("KG 27",             "Germany"),
            new("KG 40",             "Germany"),
            new("KG 51",             "Germany"),
            new("KG 53",             "Germany"),
            new("KG 54",             "Germany"),
            new("KG 55",             "Germany"),
            new("KG 76",             "Germany"),
            new("LG 1",              "Germany"),
            new("LG 2",              "Germany"),
            new("StG 1",             "Germany"),
            new("StG 2 Immelmann",   "Germany"),
            new("StG 3",             "Germany"),
            new("StG 51",            "Germany"),
            new("StG 77",            "Germany"),
            new("ZG 2",              "Germany"),
            new("ZG 26",             "Germany"),
            new("ZG 76",             "Germany"),
        ],
        ["Italy"] =
        [
            new("56° Stormo CT",            "Italy"),
            new("172° Gruppo CT",           "Italy"),
            new("236° Squadriglia CT",      "Italy"),
        ],
    };

    public static IEnumerable<Squadron> GetSquadronsByCountry(string country)
        => s_squadrons.TryGetValue(country, out var list) ? list : [];

    // ── Weather presets ───────────────────────────────────────────────────────

    public static readonly WeatherPreset[] WeatherPresets =
    [
        new("Clear",              "No cloud cover. Excellent visibility, 30+ km.",          null),
        new("Haze",               "Surface haze limits horizon to 15–20 km, no cloud.",     null),
        new("Scattered Cumulus",  "3–4 oktas. Fair-weather cumulus. Good visibility.",       5),
        new("Medium Clouds",      "5–6 oktas broken cumulus. Variable visibility.",          6),
        new("Overcast",           "7–8 oktas. Solid ceiling restricting altitude ops.",      4),
        new("Heavy Overcast",     "8–10 oktas. Very low ceiling, visibility reduced.",       2),
        new("Thin Cirrus",        "High thin cirrus at 15–20 kft; little low-level effect.", 18),
        new("Rain Showers",       "Scattered showers, poor visibility in precipitation.",    3),
        new("Heavy Rain",         "Continuous rain. Very poor visibility; instrument conds.",2),
        new("Thunderstorms",      "Cumulonimbus, severe turbulence. Extreme hazard.",        4),
        new("Night — Clear",      "Clear night sky. Stars visible.",                        null),
        new("Night — Overcast",   "Overcast night. Minimal ambient light.",                 3),
    ];
}
