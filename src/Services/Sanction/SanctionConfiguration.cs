namespace SwiftlyAC.Services.Bans;

public class SanctionConfiguration
{
    public string BanCommand {get;set;} = "sw_ban {steamid} 0 \"{reason}\"";
    public Dictionary<string, string> Placeholders {get;set;} = new()
    {
        ["{steamid}"] = "SteamID of the player to ban",
        ["{reason}"] = "Reason for the ban",
        ["{playerid}"] = "Player ID of the player to ban"
    };
}