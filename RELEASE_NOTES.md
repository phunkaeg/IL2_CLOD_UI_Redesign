# Cliffs of Dover UI Overhaul - Team Fusion Preview

## Release Package

Recommended release asset:

`TeamFusion-CliffsOfDoverUiOverhaul-win-x64-models-fixed-v2.zip`

This is a Windows x64 self-contained preview build. It includes the executable, UI assets, 3D model assets, map images, and map calibration data needed to run the prototype without installing the .NET SDK.

Launch:

`CliffsOfDoverUiOverhaul.exe`

## What This Build Contains

- Recreated IL-2 Sturmovik: Cliffs of Dover front-end flow in WPF/XAML/C#.
- Main menu, single player, campaign selector, campaign board, single mission, quick mission, options, multiplayer, pilot, aircraft, and loadout screens.
- Campaign selector with campaign poster preview, theatre filtering, campaign description parsing, and cached discovery during a session.
- Campaign board with day timeline, time-of-day mission cards, available-flight selection, briefing, gallery, pilot log, met report, and permanent navigation map panel.
- Mission parsing support for vanilla Cliffs of Dover campaign folders, Tobruk campaign folders, quick missions, single missions, and nested ATAG_Lenny-style campaign structures.
- Strait of Dover and Tobruk map handling with calibration data.
- Route plotting on the map for the selected playable flight.
- Enemy sighting extraction from briefing text, displayed as map indicators where recognised.
- Side-aware UI colouring for RAF, Luftwaffe, and Regia Aeronautica flights.
- Pilot log prototype for manual sortie reports, designed so game-side data can later fill entries automatically.
- 3D preview panels for aircraft and pilot models.
- Settings overlay for game root/documents paths, rescanning, parser report, and export utilities.

## Important Fixes In This Release

- Fixed packaged EXE model loading so the aircraft and pilot OBJ assets remain external beside the executable instead of being hidden inside the single-file EXE bundle.
- Added a cleaned pilot model asset chain:
  - `Assets/3d/ww2_Pilot.obj`
  - `Assets/3d/ww2_Pilot.mtl`
  - `Assets/3d/textures/ww2_Pilot.png`
- Removed the brittle old absolute texture path from the pilot material file.
- Improved model loader feedback so missing or failed model loads show a visible message instead of silently falling back to cube placeholders.
- Kept the repository root focused on the normal development project so `dotnet run` works from the repository folder again.
- Moved optional release packaging into `Packaging/CliffsOfDoverUiOverhaul.Package.csproj`.
- Kept source dependencies as normal NuGet package references for easier GitHub cloning.

## Known Limitations

- The executable is not code-signed, so Windows SmartScreen may warn on first launch.
- Some parser edge cases remain likely because vanilla campaigns, quick missions, single missions, and custom campaigns use different structures.
- Campaign progression is currently prototype-level. Vanilla campaign state files are parsed/considered, but game-side success/failure integration is not connected.
- Pilot log entries are local prototype data and are not yet written back into the game.
- Mission launch buttons are UI placeholders until connected to the real game front-end/game logic.
- Parser accuracy depends on file naming, `.mis`, `.briefing`, `.cs`, and campaign folder conventions.

## Suggested Team Fusion Review Areas

- Confirm whether the WPF/XAML screens can be mapped into the existing front-end architecture without requiring major game-side rewrites.
- Review parser output against known vanilla campaign and Tobruk campaign structures.
- Review proposed integration contracts in `INTERNAL_GAME_TEAM_HANDOFF.md` and `GAME_FRONTEND_INTEGRATION_GUIDE.md`.
- Identify which game-side services would own:
  - campaign discovery
  - campaign progress state
  - mission launch
  - pilot log/autofill
  - aircraft/loadout data
  - localization
  - official map calibration

## Source Notes

Primary development project:

`PlaneLoadoutWpfTest.csproj`

Optional release packaging project:

`Packaging/CliffsOfDoverUiOverhaul.Package.csproj`

Main implementation folders:

- `Screens/`
- `Services/`
- `Controls/`
- `Assets/`

Supporting documentation:

- `CAMPAIGN_BOARD_SPEC.md`
- `MISSION_DATA_PARSING.md`
- `GAME_FRONTEND_INTEGRATION_GUIDE.md`
- `INTERNAL_GAME_TEAM_HANDOFF.md`
- `Missions_Folders.txt`
