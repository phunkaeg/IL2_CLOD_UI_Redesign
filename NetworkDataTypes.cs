namespace PlaneLoadoutWpfTest;

/// <summary>Represents a row in the lobby browser list.</summary>
public sealed class LobbyEntry
{
    public string Name    { get; set; } = "";
    public string Type    { get; set; } = "";
    public string Map     { get; set; } = "";
    public string Players { get; set; } = "";
}

/// <summary>Represents a row in the server browser list.</summary>
public sealed class ServerEntry
{
    public string Name    { get; set; } = "";
    public string Type    { get; set; } = "";
    public string Map     { get; set; } = "";
    public string Players { get; set; } = "";
    public string Ping    { get; set; } = "";
}
