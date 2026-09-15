namespace SwiftlyAC.Services.ConVars;

public class ConVarsConfiguration
{
    public bool Enabled { get; set; } = true;
    public string CheckInterval { get; set; } = "1m";
    public string DelayInterval { get; set; } = "15s";
    
    public bool DebugLogs { get; set; } = false;
}