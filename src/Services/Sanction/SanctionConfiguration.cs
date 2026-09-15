namespace SwiftlyAC.Services.Sanction;

public class SanctionConfiguration
{
    public string BanCommand {get;set;} = "sw_ban {steamid} 0 {reason}";
    public string KickCommand {get;set;} = "sw_kick {steamid}";
    public Dictionary<string, string> Placeholders {get;set;} = new()
    {
        ["{steamid}"] = "SteamID of the player",
        ["{reason}"] = "Reason for the sanction",
        ["{playerid}"] = "Player ID of the player",
        ["{userid}"] = "User ID of the player"
    };
    public bool DebugLogs {get; set;} = false;
    public string BypassPermission {get;set;} = "swiftlyac.bypass";
}