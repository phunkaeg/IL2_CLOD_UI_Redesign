# Campaign Board Technical Specification

This document describes the current WPF campaign selector and campaign board prototype.

## Purpose

The campaign board provides a more readable operational view over Cliffs of Dover campaign content:

1. Discover campaign folders from configured game/documents roots.
2. Parse vanilla campaign folders, Tobruk campaigns, and nested custom campaign structures.
3. Build a campaign selector with poster/description previews.
4. Show a day/time campaign timeline.
5. Let the player choose a time slot and then a playable flight.
6. Show briefing, gallery, pilot log, met report, and a calibrated route map for the selected flight.

The prototype currently parses files directly. A game-integrated version should place this parser behind a game-owned campaign data service.

## Main Source Files

- `Screens/CampaignSelectorScreen.xaml`
- `Screens/CampaignSelectorScreen.xaml.cs`
- `Screens/CampaignScreen.xaml`
- `Screens/CampaignScreen.xaml.cs`
- `Services/CampaignSelectionService.cs`
- `Services/CampaignBoardService.cs`
- `Services/CampaignBoardSession.cs`
- `Services/CampaignBoardExportService.cs`
- `Services/CampaignIntegrationContracts.cs`

## Campaign Selector

The selector displays:

- Campaign list on the left.
- Poster image on the right.
- Description below the poster.
- Theatre toggle: All, Strait, Tobruk.
- Back, Rescan, Next controls.

Discovery supports:

- Vanilla `campaigns.ini` entries.
- Vanilla campaign folders with `Campaign.ini`, `Campaign.briefing`, `Campaign.mis`, and `Poster.bmp`.
- Campaign folders with a folder-name briefing file.
- Nested custom campaign folders, including ATAG_Lenny-style content.
- Custom master folders containing date folders or time-of-day folders.

Discovery should not list the entire `parts\bob\mission\campaign` or `parts\tobruk\mission\campaign` folder as a fake campaign.

The selector caches discovery once per app session. Pressing Back from the campaign board should not rescan everything. Manual rescan should be explicit.

## Campaign Board Model

The board model contains:

- Campaign title, source root, period, and map.
- Days.
- Missions/time slots.
- Playable roles.
- Routes and waypoints.
- Briefing slides/gallery images.
- Weather/met report text.
- Parser warnings/diagnostic data where available.

## Directory And File Parsing

The parser reads:

- `.mis` mission files.
- Paired `.briefing` files.
- Campaign-level `.briefing` files.
- `campaigns.ini`.
- `Campaign.ini`.
- Poster images.
- Briefing/gallery images.
- Optional `.cs` mission scripts as supporting context.

The `.mis` file is treated as the primary mission data source. The paired `.briefing` file is the primary player-facing briefing source.

## Mission Pairing

For a mission file:

```text
1400 - Me-109 - 2LG2 - Bomber Escort.mis
```

The parser first looks for:

```text
1400 - Me-109 - 2LG2 - Bomber Escort.briefing
```

If no exact match exists, it can fall back to a generic briefing file in the same time-slot folder.

This is important for Lenny-style folders, where one time slot can contain multiple flyable mission files for RAF and Luftwaffe flights.

## Vanilla Campaign Notes

Vanilla campaigns often use compact filenames:

```text
01.mis
01.briefing
109 0710 Convoy01.mis
```

The parser must not treat the aircraft number `109` as the mission time. If the filename contains both aircraft and time, the time token is usually the later HHMM token.

Vanilla briefing sections such as `Intro`, `Success`, and `Failure` are narrative state sections, not playable flights.

## Briefing Section Rules

Narrative sections are skipped as playable roles:

```text
Info
Intro
Introduction
Success
Failure
Fail
Background
Objectives
Results
Debrief
Summary
Notes
```

Playable sections are matched against air groups using:

- Section key.
- `<Name>` text.
- Unit/squadron/gruppe text.
- Aircraft text.
- Air group id.
- Fuzzy normalised token matching.

## Time And Date Rules

Date/time extraction priority:

1. Explicit briefing text date/time.
2. Credible filename HHMM token.
3. `[MAIN] TIME` from `.mis`.
4. Folder date/time.
5. Campaign order fallback.

All displayed times should be normalised as `HH:mm`.

When a campaign has no day title, use the date rather than repeating technical folder names like `campaign_de`.

## Side Inference

Sides are inferred from unit names, aircraft, air group ids, and briefing text.

Supported display sides:

- RAF
- Luftwaffe
- Regia Aeronautica
- Unknown

Flight cards should be side-coloured:

- RAF: green tint.
- Luftwaffe: blue-grey tint.
- Regia Aeronautica: Italian/green tint.

Time-of-day cards should use the neutral theme, not side colours.

## Filters

Current operation filters:

- Side.
- Unit/squadron/Gruppe.
- Mission type.
- Aircraft type.
- Map/theatre.

Filters affect:

- Available flights.
- Time slots.
- Day timeline.

If a filter leaves a day with no available flights, that day should be hidden from the timeline.

## Map Behaviour

Maps:

- Strait of Dover: `Assets/strait_of_dover_map.jpg`
- Tobruk: `Assets/tobruk_map.jpg`

Calibration file:

```text
Map_Calibration_data.txt
```

The map panel is permanent on the right of the campaign board.

Only the route for the selected available flight should be displayed. Enemy routes should not be shown unless the selected flight is an enemy-side playable flight.

Map controls:

- Fit selected route.
- Zoom in/out.
- Mouse wheel zoom.
- Drag pan.
- Smooth but non-jittery zoom.

The map should auto-frame the selected route where possible.

## Enemy Sightings

The parser attempts to extract enemy sighting/target locations from briefing prose:

```text
plots building up over ...
enemy spotted over ...
bandits spotted over ...
target ...
bombing target ...
```

If a recognised location maps to a calibration point, show a semi-transparent marker with a themed tooltip.

## Pilot Log

The pilot log is a prototype manual sortie report. Future game integration should let the game autofill entries from sortie telemetry and career state.

## Gallery

Briefing/gallery images are extracted from briefing slides and image files beside missions. The gallery view should behave as a slideshow when multiple images are available.

## Export And Diagnostics

The UI Overhaul settings flow can export:

- Campaign board JSON.
- Parser diagnostics Markdown report.

These outputs are intended for comparing prototype parser output against the real game’s campaign data.

## Integration Guidance

For production integration:

- Keep the WPF screens and theme as presentation.
- Move parser/file-system access behind interfaces.
- Let the game own campaign progress, unlock state, pilot identity, mission launch, and persistence.
- Use game asset services instead of hard-coded file paths.
- Use game map projection/calibration where possible.

See:

- `GAME_FRONTEND_INTEGRATION_GUIDE.md`
- `INTERNAL_GAME_TEAM_HANDOFF.md`
- `Services/CampaignIntegrationContracts.cs`
