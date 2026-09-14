using SwiftlyAC.Services.Bans;

public class Configuration
{
    public string Prefix {get;set;} = "[[blue]SwiftlyAC[default]]";
    public SanctionConfiguration Sanctions {get;set;} = new();
}