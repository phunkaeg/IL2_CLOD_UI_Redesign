# Mission Data Parsing - Current WPF Prototype

This document describes the current WPF/C# parser behaviour used by the UI overhaul prototype. It replaces the older React/JSON notes from the first campaign-board experiment.

## Parser Entry Points

The current parser code lives mainly in:

- `Services/CampaignSelectionService.cs`
- `Services/CampaignBoardService.cs`
- `Services/MissionScannerService.cs`
- `Services/MissionPathResolver.cs`
- `Services/CampaignBoardExportService.cs`

The UI does not depend on a prebuilt `campaign-data.json` file. It scans configured Cliffs of Dover game/documents folders and builds display models in memory.

## Search Roots

Given a game root, the scanner checks:

```text
missions\Single\
parts\bob\mission\campaign\
parts\bob\mission\Quick\
parts\bob\missions\Single\
parts\tobruk\mission\campaign\
parts\tobruk\mission\Quick\
parts\tobruk\missions\Single\
```

Given a documents root, the scanner checks:

```text
1C SoftClub\il-2 sturmovik cliffs of dover\missions\single\
1C SoftClub\il-2 sturmovik cliffs of dover\mission\campaign\
1C SoftClub\il-2 sturmovik cliffs of dover\mission\quick\
```

Multiplayer paths are documented in `Missions_Folders.txt`, but are not currently used by the campaign browser.

## Campaign Discovery

Campaign discovery supports three broad structures:

1. Vanilla campaigns registered by `campaigns.ini`.
2. Vanilla campaign folders with `Campaign.ini`, `Campaign.briefing`, `Campaign.mis`, and `Poster.bmp`.
3. Nested custom campaign folders, including ATAG_Lenny-style folders with date/day folders or time-slot folders below a master campaign folder.

The selector deliberately avoids adding the entire `parts\bob\mission\campaign` or `parts\tobruk\mission\campaign` folder as a fake campaign entry. It should only list actual campaign folders.

Campaign selector data is cached for the session. It should not rescan every time the user presses Back. Manual rescans are available from the settings flow.

## Campaign Metadata

The selector tries to detect:

- Campaign title.
- Theatre/map: Strait of Dover or Tobruk.
- Date range.
- Mission count.
- Campaign progress where vanilla state data is available.
- Poster image.
- Campaign description.

Description files may be named:

- `Campaign.briefing`
- `<campaign-folder-name>.briefing`
- Other campaign-level briefing files in some vanilla campaign folders.

Poster files are usually:

- `Poster.bmp`
- Sometimes fallback images such as `CampaignMain.jpg`.

## Mission Pairing

Mission data is usually made from paired files:

```text
mission.mis
mission.briefing
mission.cs
mission.misroads
```

The `.mis` file is the primary mission data source. The matching `.briefing` file provides player-facing briefing text and role sections. The `.cs` file may contain game-side mission logic, triggers, and success/failure criteria, but the prototype only uses it lightly as supporting text when needed. `.misroads` is currently not a primary parser input.

For custom Lenny-style missions, one time-slot folder can contain several flyable mission files for different sides/units. The parser treats each `.mis` + paired `.briefing` as a potential playable flight, rather than only reading the generic time-slot file.

## Vanilla Campaign Differences

Vanilla campaigns do not always use date-folder/time-folder naming. They may use mission names such as:

```text
01.mis
01.briefing
109 0710 Convoy01.mis
```

The parser must avoid treating aircraft numbers such as `109` as mission time. Time detection priority is:

1. Briefing text date/time if explicit and credible.
2. Mission filename time token where the filename clearly contains an HHMM value.
3. `[MAIN] TIME` value from `.mis`, interpreted as game mission time.
4. Fallback ordering.

Some `.mis` files store time as decimal hours or other numeric values. The parser normalises display time to `HH:mm`.

## Briefing Parsing

Briefing files use section-style tags. The parser extracts:

- Section key.
- `<Name>`.
- `<Description>`.
- `<Slide>` and `<Caption>` pairs.
- General briefing text.
- Role-specific briefing text.

Narrative sections are not playable roles. Examples:

```text
Info
Intro
Success
Failure
Background
Objectives
Summary
Debrief
```

Playable sections are matched to air groups using section names, unit names, aircraft names, air group ids, and fuzzy token matching.

## Side Inference

The parser infers side from unit names, air group ids, aircraft names, and briefing text.

Recognised sides:

- RAF
- Luftwaffe
- Regia Aeronautica
- Unknown

Typical RAF signals:

```text
RAF, RAAF, Sqn, Squadron, Spitfire, Hurricane, Blenheim, Defiant, Beaufighter, Gladiator
```

Typical Luftwaffe signals:

```text
Luftwaffe, JG, KG, LG, StG, ZG, Staffel, Gruppe, Bf 109, Bf 110, He 111, Do 17, Ju 87, Ju 88
```

Typical Regia Aeronautica signals:

```text
Regia, Aeronautica, Italian, Italia, Squadriglia, Gruppo, CR.42, G.50, BR.20, C.200, Fiat, Macchi
```

Regia is checked before RAF so `RA` and Italian groups do not become false RAF hits.

## Aircraft Identification

Aircraft names are cleaned for display and grouped for filtering.

Examples:

```text
HurricaneMkI          -> Hurricane Mk I
SpitfireMkIa          -> Spitfire Mk Ia
Bf-109E-3             -> Bf 109E-3
Aircraft.Bf_109E_4    -> Bf 109E-4
```

Filters use both aircraft family and variant where enough data exists.

## Mission Type Inference

Mission type is inferred from mission title, briefing text, aircraft, unit, role, and route text.

Current labels include:

- Anti-shipping
- Bomber / Strike
- Escort / Cover
- Fighter / Patrol
- Reconnaissance
- Search & Rescue
- Training
- Circus
- Rodeo
- Rhubarb
- Ramrod
- Noball
- Other / Special

Filtering by mission type should also hide days on the campaign timeline when no matching flights remain on that day.

## Routes And Waypoints

Routes are parsed from aircraft air group sections and matching waypoint sections in `.mis` files.

The campaign board shows the route for the selected available flight, not every route in the mission. This avoids showing enemy route plans when the player has selected a friendly flight.

Each route stores:

- Air group id.
- Unit/briefing label.
- Side.
- Aircraft.
- Formation.
- Skill.
- Waypoints.

The parser deduplicates repeated routes by comparing ids, aircraft, unit labels, and waypoint signatures.

## Map Selection

Theatre/map detection uses folder paths, mission text, campaign metadata, and known Tobruk/Battle of Britain signals.

Maps:

- Strait of Dover: `Assets/strait_of_dover_map.jpg`
- Tobruk: `Assets/tobruk_map.jpg`

Map filtering is exposed in the campaign board operations filter.

## Map Calibration

Calibration data lives in:

```text
Map_Calibration_data.txt
```

It contains named reference points for Strait of Dover and Tobruk.

The route mapper uses calibration points to convert game coordinates to map pixels. When calibration is unavailable or a route still looks invalid, the UI may fall back to a schematic fit so the route remains visible, but calibrated routes are preferred.

## Enemy Sightings

Briefing text is scanned for phrases such as:

- enemy spotted over ...
- bandits spotted over ...
- plots building up over ...
- bombing target ...
- target designated ...

When a recognised location can be mapped, the UI can show a semi-transparent enemy sighting marker on the map with a themed tooltip.

This is heuristic and should be treated as a helpful display aid, not authoritative game intelligence.

## Single Mission Parsing

Single missions use `MissionScannerService`.

The scanner tries to detect:

- Title.
- Map/theatre.
- Time.
- Briefing summary.
- Banner image.
- Category/source.
- Mission path.
- Briefing path.
- Route waypoints for map display.

Briefing tag blocks such as `[Info]` are cleaned into readable paragraphs for the UI.

## Export And Diagnostics

Campaign JSON and parser reports are available through the UI Overhaul settings flow once campaign data has been scanned.

Diagnostics are useful for comparing prototype parser output against the real game’s understanding of campaigns, missions, roles, and routes.

## Integration Direction

For real game integration, the parser should sit behind a game-owned campaign/mission provider interface. The UI should ask for campaign data, mission data, route data, and launch permissions; it should not own final campaign progression or mission launch rules.
