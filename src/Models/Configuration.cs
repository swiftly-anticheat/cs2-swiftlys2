using SwiftlyAC.Services.Sanction;
using SwiftlyAC.Services.GameEvents;
using SwiftlyAC.Services.Notifications;
using SwiftlyAC.Services.ConVars;

public class Configuration
{
    public string Prefix {get;set;} = "[[lightblue]SwiftlyAC[default]]";
    public SanctionConfiguration Sanctions {get;set;} = new();
    public GameEventsConfiguration GameEvents {get;set;} = new();
    public NotificationsConfiguration Notifications {get;set;} = new();
    public ConVarsConfiguration ConVars {get;set;} = new();
}