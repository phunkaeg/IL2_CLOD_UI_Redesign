namespace PlaneLoadoutWpfTest.Services;

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

public sealed record MissionLaunchRequest(
    string CampaignId,
    string MissionId,
    string MissionFile,
    string PlayerAirGroupId,
    string Aircraft,
    string Side);

public sealed record MissionLaunchResult(
    bool Success,
    string Message);
