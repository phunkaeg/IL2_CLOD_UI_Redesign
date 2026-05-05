# Internal Game Team Handoff

This project should be treated as a campaign-browser prototype, parser reference, and diagnostic tool. It is not intended to be dropped directly into the IL-2 Cliffs of Dover front end.

## Why This Exists

The prototype explores a campaign board workflow:

- Scan campaign folders, `.mis` files, `.briefing` files, images, and `campaigns.ini`.
- Build a day/time timeline.
- Detect playable sides, units, aircraft, mission types, briefing slides, routes, and map data.
- Present a selectable sortie board with briefing, map, gallery, and pilot log views.
- Export the parsed campaign model for inspection.

The useful handoff value is the data shape and parsing behavior, not the exact WPF screen implementation.

## Most Useful Files

- `Services/CampaignBoardService.cs`
  - Parser and display-model builder.
  - Useful for understanding which `.mis` and `.briefing` sections the prototype expects.

- `Services/CampaignBoardExportService.cs`
  - Produces JSON and Markdown diagnostics from the parsed model.
  - This is the safest part to use as a comparison tool.

- `Services/CampaignIntegrationContracts.cs`
  - Suggested interface boundary between UI, campaign data, mission launch logic, and pilot log persistence.
  - This is a reference contract, not an instruction that the game must implement these exact types.

- `Screens/CampaignScreen.xaml` and `Screens/CampaignScreen.xaml.cs`
  - UX reference only.
  - Shows the intended player workflow.

- `GAME_FRONTEND_INTEGRATION_GUIDE.md`
  - More detailed notes on how a clean integration boundary might look.

## New Diagnostic Outputs

The campaign screen now has two export buttons:

- `EXPORT JSON`
  - Writes the full parsed campaign model to a `.json` file.
  - Useful for checking exactly what the prototype detected.

- `REPORT`
  - Writes a Markdown parser report.
  - Includes campaign count, day count, mission count, side classifications, mission types, and warnings.

These outputs let the internal team compare the prototype parser against the real game state without adopting the UI code.

## Suggested Integration Boundary

If the game ever gains a clean campaign browser boundary, the UI only needs a small set of services:

```csharp
public interface ICampaignDataProvider
{
    IReadOnlyList<CampaignBoardCampaign> GetCampaigns();
    CampaignBoardCampaign? GetCampaign(string campaignId);
    CampaignMission? GetMission(string campaignId, string missionId);
}

public interface IMissionLaunchService
{
    MissionLaunchResult LaunchMission(MissionLaunchRequest request);
}

public interface ICampaignPilotLogStore
{
    IReadOnlyList<PilotLogEntry> GetEntries(string pilotId, string campaignId);
    void SaveEntry(string pilotId, PilotLogEntry entry);
}
```

The current game may already have equivalents with different names. The important idea is separation:

- The UI asks for campaign data.
- The game owns mission launch and progression.
- The game owns pilot identity, career state, difficulty, aircraft ownership, and persistence.
- The parser is only a fallback/reference layer.

## What Not To Transplant Directly

Avoid copying the prototype screen into the game as-is if the existing front end and game logic are tightly coupled. That would likely require a large refactor.

Avoid making the screen parse mission files directly inside production game UI. If the parser is useful, put it behind a game-owned provider interface.

Avoid letting the prototype decide final campaign progression, unlock state, pilot career state, or mission launch rules. Those should remain owned by the game.

## Data The UI Needs

For each campaign:

- Stable campaign id.
- Display title.
- Period/date range.
- Available days and/or times.
- Map identity and dimensions.
- Mission count.

For each mission:

- Stable mission id.
- Source mission file.
- Date and time.
- Title.
- Mission type.
- Briefing text.
- Weather text.
- Playable roles.
- Aircraft routes and waypoints.
- Briefing slides/gallery images.

For each playable role:

- Stable role id.
- Side.
- Unit or Gruppe.
- Aircraft type.
- Air group id used by the game.
- Human-readable objective/description.

For launching:

- Campaign id.
- Mission id/source file.
- Player air group id.
- Selected aircraft/side.
- Any game-owned difficulty, pilot, career, and progression state.

## Current Parser Assumptions

The parser currently assumes:

- Campaigns may be organized by date folders, time folders, fidelity folders, or direct mission folders.
- `.briefing` files are paired to `.mis` files by matching stem where possible.
- Narrative briefing sections such as `Info`, `Intro`, `Success`, and `Failure` are not playable roles.
- RAF/Luftwaffe/Regia classification is inferred from unit ids, group names, aircraft types, and briefing text.
- Routes are parsed from aircraft sections and matching `_Way` sections.
- Map calibration is approximate and should be replaced by game-owned map projection logic if integrated.

## Recommended Use By The Internal Team

1. Run the prototype against known campaign folders.
2. Export JSON and diagnostics.
3. Compare detected missions, playable roles, sides, aircraft, and routes against what the game itself believes.
4. Use any mismatches as parser bug reports or as evidence of where the real game has hidden logic the prototype cannot infer.
5. Treat the UI as a design reference, not production-ready integration code.

