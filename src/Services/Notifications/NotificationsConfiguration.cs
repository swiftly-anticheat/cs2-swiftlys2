namespace SwiftlyAC.Services.Notifications;

public class ChatNotification
{
    public bool Enabled {get;set;} = true;
    public string Permission {get;set;} = "swiftlyac.notifications.chat";
}

public class NotificationsConfiguration
{
    public bool Enabled {get;set;} = true;
    public string DetailsPermission {get;set;} = "swiftlyac.notifications.details";
    public bool DebugLogs {get;set;} = false;
    public ChatNotification Chat {get;set;} = new();
}