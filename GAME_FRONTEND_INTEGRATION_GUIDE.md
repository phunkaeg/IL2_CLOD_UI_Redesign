# IL-2 Cliffs of Dover Front-End Integration Guide

This project is intentionally written in WPF/XAML/C# so the UI can move toward the real IL-2 Cliffs of Dover front end instead of remaining a standalone companion app. The current code is a prototype shell with real parsers and themed screens. For game integration, treat the UI as a presentation layer and replace the prototype service implementations with game-backed services.

## Current Architecture Snapshot

- `Screens/*Screen.xaml` and `Screens/*Screen.xaml.cs` contain the visual pages.
- `Theme.xaml` contains shared styling, colors, button templates, list styles, combo boxes, tabs, scrollbars, and panel chrome.
- `Services/NavigationService.cs` is a simple WPF screen stack.
- `Services/CampaignBoardService.cs` scans and parses campaign `.mis`, `.briefing`, and `campaigns.ini` data into a display model.
- `Services/MissionScannerService.cs` is a lighter single-mission scanner used by other menu screens.
- `Services/MissionCatalog.cs` is an in-memory catalog populated from configured folder paths.
- `AppSettings.cs` and `AppSettingsService.cs` store user-configured paths and UI preferences.

## Main Recommendation

Before transplanting into the game, introduce an interface layer between the UI and any real game logic. Keep XAML and screen layout mostly as-is, but do not let screens call file-system scanners, local settings, or launch logic directly.

Recommended shape:

```csharp
public interface IGameFrontendHost
{
    IGameNavigationService Navigation { get; }
    IMissionLibraryService MissionLibrary { get; }
    ICampaignLibraryService CampaignLibrary { get; }
    IMissionLaunchService MissionLauncher { get; }
    IPlayerProfileService PlayerProfile { get; }
    IPilotLogService PilotLog { get; }
    IGameAssetService Assets { get; }
    IGameSettingsService Settings { get; }
}
```

The prototype app can provide file-system implementations. The game can provide engine-backed implementations.

## Integration Ports

### 1. Navigation

Current prototype:

- `NavigationService.GoTo(new SomeScreen())`
- `NavigationService.Back()`
- WPF `UserControl` stack

Game integration target:

```csharp
public interface IGameNavigationService
{
    void GoTo(string screenId, object? parameter = null);
    void Back();
    void Replace(string screenId, object? parameter = null);
    bool CanGoBack { get; }
}
```

Suggested screen ids:

- `main-menu`
- `single-player`
- `campaign-board`
- `quick-mission`
- `single-mission`
- `plane-loadout`
- `pilot-options`
- `realism`
- `video-options`
- `audio-options`
- `controls`
- `multiplayer`

The WPF prototype can still map these ids to `UserControl` instances. The game may instead map them to an existing front-end state machine.

### 2. Campaign Library

Current prototype:

- `CampaignBoardService.ScanCampaignRoot(path)`
- Reads `.mis`, `.briefing`, `campaigns.ini`, and local images directly from disk.

Game integration target:

```csharp
public interface ICampaignLibraryService
{
    Task<IReadOnlyList<CampaignBoardCampaign>> GetCampaignsAsync(CancellationToken ct);
    Task<CampaignBoardCampaign?> GetCampaignAsync(string campaignId, CancellationToken ct);
    Task RefreshAsync(CancellationToken ct);
}
```

For the game, `GetCampaignsAsync` should ideally read from the real installed campaign registry, not a user-selected folder. The parser in `CampaignBoardService` is useful as a fallback or migration parser, but it should be behind the interface.

Recommended future additions to the model:

```csharp
public sealed class CampaignBoardCampaign
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string TheatreId { get; init; } = "";
    public string SourcePackageId { get; init; } = "";
    public bool IsOfficialContent { get; init; }
}
```

### 3. Mission Launch

Current prototype:

- Launch buttons are not yet connected.
- Mission cards currently represent parsed data only.

Game integration target:

```csharp
public interface IMissionLaunchService
{
    Task<LaunchValidationResult> ValidateAsync(MissionLaunchRequest request, CancellationToken ct);
    Task LaunchAsync(MissionLaunchRequest request, CancellationToken ct);
}

public sealed class MissionLaunchRequest
{
    public string CampaignId { get; init; } = "";
    public string MissionId { get; init; } = "";
    public string SourceMissionPath { get; init; } = "";
    public string SelectedAirGroupId { get; init; } = "";
    public string SelectedAircraft { get; init; } = "";
    public string SelectedSide { get; init; } = "";
    public string PlayerProfileId { get; init; } = "";
    public IReadOnlyDictionary<string, string> Options { get; init; } = new Dictionary<string, string>();
}

public sealed class LaunchValidationResult
{
    public bool CanLaunch { get; init; }
    public string Message { get; init; } = "";
}
```

The launch implementation should be owned by the game. It can:

- select the correct `.mis` file
- bind the chosen player air group
- apply aircraft/loadout choices
- set difficulty or realism flags
- move from front-end UI to the simulation loading state

The UI should only build a `MissionLaunchRequest`.

### 4. Mission And Aircraft Data

Current prototype:

- Aircraft and units are inferred from `.mis` text.
- Loadout screens use prototype data and assets.

Game integration target:

```csharp
public interface IMissionLibraryService
{
    Task<IReadOnlyList<GameMissionSummary>> GetSingleMissionsAsync(CancellationToken ct);
    Task<GameMissionDetail?> GetMissionDetailAsync(string missionId, CancellationToken ct);
}

public interface IAircraftLibraryService
{
    Task<IReadOnlyList<AircraftDefinition>> GetAircraftAsync(CancellationToken ct);
    Task<AircraftLoadoutDefinition?> GetLoadoutAsync(string aircraftId, CancellationToken ct);
}
```

The game should supply canonical aircraft ids and display names. The parser can keep `RawAircraft` for diagnostics, but screens should prefer game-provided aircraft definitions when available.

### 5. Assets

Current prototype:

- XAML references local `Assets/*`.
- Campaign slides use absolute image paths from mission folders.
- Map selection is inferred from campaign text.

Game integration target:

```csharp
public interface IGameAssetService
{
    Uri GetThemeAsset(string assetId);
    Uri GetMapImage(string theatreId);
    Uri? GetMissionImage(string missionId, string imageId);
    Stream? OpenAssetStream(string assetId);
}
```

Recommended asset ids:

- `map.bob.strait-of-dover`
- `map.tobruk`
- `logo.cliffs-of-dover`
- `roundel.raf`
- `roundel.luftwaffe`
- `roundel.regia-aeronautica`
- `texture.panel`
- `texture.rivets`

Avoid hard-coded relative paths in final game code. Use the asset service so official game packaging, DLC content, localization, and mod folders can all resolve assets consistently.

### 6. Player Profile And Pilot Log

Current prototype:

- Pilot log UI is a draft text area.
- No persistent sortie result contract exists yet.

Game integration target:

```csharp
public interface IPlayerProfileService
{
    Task<IReadOnlyList<PlayerProfile>> GetProfilesAsync(CancellationToken ct);
    Task<PlayerProfile?> GetActiveProfileAsync(CancellationToken ct);
}

public interface IPilotLogService
{
    Task<IReadOnlyList<PilotLogEntry>> GetEntriesAsync(string profileId, CancellationToken ct);
    Task SaveEntryAsync(PilotLogEntry entry, CancellationToken ct);
    Task<PilotSortieTelemetry?> GetLatestSortieTelemetryAsync(CancellationToken ct);
}
```

The game can pre-fill the pilot log from actual sortie results:

- takeoff time
- landing time
- aircraft
- airfield
- damage
- kills or claims
- ground or shipping claims
- player status
- mission success/failure state

### 7. Settings

Current prototype:

- Stores paths in `%AppData%`.
- User browses to mission folders.

Game integration target:

```csharp
public interface IGameSettingsService
{
    T Get<T>(string key, T fallback);
    void Set<T>(string key, T value);
    event Action<string>? SettingChanged;
}
```

In the game, campaign and mission paths should usually come from the installation/content manager, not from the user. The current path settings remain useful for development builds, mod testing, and external campaign authoring.

## Campaign Parser Notes

`CampaignBoardService` already performs useful real-world parsing:

- campaign folder detection
- `campaigns.ini` parsing
- primary `.mis` selection
- `.briefing` section parsing
- slide/image extraction
- air group detection
- waypoint extraction
- player-flyable role detection
- side inference for RAF, Luftwaffe, and Regia Aeronautica
- aircraft display-name cleanup
- mission type inference
- date/time extraction from briefing prose
- day grouping for timeline display

Recommended hardening before game integration:

1. Add stable ids to `CampaignBoardCampaign`, `CampaignDay`, `CampaignMission`, and `CampaignRole`.
2. Preserve raw parser diagnostics for debugging bad community campaigns.
3. Return parse warnings instead of silently skipping malformed sections.
4. Make file reads encoding-aware. Older mission files may not be UTF-8.
5. Add cancellation support for large campaign scans.
6. Move scanning off the UI thread everywhere.
7. Split parser code from file-system discovery so the game can feed packaged content streams.

Suggested parser split:

```csharp
public interface ICampaignSource
{
    string SourceId { get; }
    IEnumerable<ICampaignFile> EnumerateFiles();
}

public interface ICampaignFile
{
    string Path { get; }
    string Name { get; }
    Stream OpenRead();
}

public sealed class CampaignParser
{
    public CampaignBoardCampaign Parse(ICampaignSource source);
}
```

That lets the same parser consume:

- loose development folders
- game installation folders
- packed game archives
- Steam Workshop/mod content
- DLC content

## Campaign Screen UI Notes

The campaign screen currently expects:

- a list of campaigns
- each campaign has days
- each day has multiple missions/sorties
- each mission has playable roles and routes
- filters are built from parsed sides, units, aircraft, and mission type

For real game integration, the screen should not call `CampaignBoardService` directly. Instead:

```csharp
var campaigns = await _host.CampaignLibrary.GetCampaignsAsync(ct);
```

The selected mission should produce a launch request:

```csharp
var request = new MissionLaunchRequest
{
    CampaignId = selectedCampaign.Id,
    MissionId = selectedMission.Id,
    SourceMissionPath = selectedMission.SourceFiles.FirstOrDefault() ?? "",
    SelectedAirGroupId = selectedRole.AirGroupId,
    SelectedAircraft = selectedRole.Aircraft,
    SelectedSide = selectedRole.Side,
    PlayerProfileId = activeProfile.Id
};

await _host.MissionLauncher.LaunchAsync(request, ct);
```

## Suggested Near-Term Refactor Plan

1. Add `Services/Interfaces/` with the host/service interfaces above.
2. Add prototype implementations:
   - `FileSystemCampaignLibraryService`
   - `FileSystemMissionLibraryService`
   - `PrototypeMissionLaunchService`
   - `PrototypeGameAssetService`
   - `JsonPilotLogService`
3. Change screens to accept services via constructor or a simple app-level service locator.
4. Keep `CampaignBoardService` as parser/model code, but stop calling it directly from screens.
5. Add `Id` fields to campaign, day, mission, role, route, and slide models.
6. Add parser warnings and expose them in a non-intrusive diagnostics panel.
7. Connect launch buttons to `IMissionLaunchService`, even if the prototype implementation only reports the request.

## Compatibility Concerns

- WPF is Windows-only and appropriate for the target, but the current project targets `net8.0-windows`. The game team should confirm whether the real front end can host .NET 8 WPF or whether it needs a different runtime profile.
- HelixToolkit is currently used for 3D model viewing. If the game already has its own aircraft render pipeline, prefer a host-provided aircraft preview control or texture stream instead of carrying HelixToolkit into the game.
- The current app uses static services heavily. For game integration, prefer instance services injected from the game host. This makes testing and engine replacement much easier.
- Current mission parsing is synchronous in places. Keep parsing away from the render/UI thread.
- Asset paths are prototype paths. Replace them with game asset ids before packaging.

## Programmer Checklist

- Provide a game implementation of `IGameFrontendHost`.
- Replace direct calls to `AppSettingsService`, `CampaignBoardService`, `MissionCatalog`, and `NavigationService` from screens.
- Supply canonical ids for campaigns, missions, air groups, aircraft, maps, and player profiles.
- Decide whether official campaign data should be parsed from existing `.mis/.briefing` files at runtime, pre-indexed at install time, or compiled into a manifest.
- Connect `IMissionLaunchService` to the actual simulation loading flow.
- Connect `IPilotLogService` to real sortie result telemetry.
- Replace hard-coded asset paths with `IGameAssetService`.
- Add localization hooks for screen text, mission labels, filters, and parser fallback strings.
- Run large campaign scans with cancellation and progress reporting.

## Bottom Line

The current UI is a good transplant candidate if it becomes host-driven. Keep the WPF screens and theme; keep the parsers as development and compatibility tools; move all real game behavior behind explicit interfaces. That will let the same front end run in prototype mode today and in game-backed mode later without rewriting the visual layer.
