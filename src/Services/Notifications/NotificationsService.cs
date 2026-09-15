using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyAC.Services.Sanction;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

namespace SwiftlyAC.Services.Notifications;

public class NotificationsService: IDisposable
{
    private ISwiftlyCore _core {get;init;}
    private ILogger<NotificationsService> _logger {get;init;}
    private NotificationsConfiguration _config {get;set;}
    private string _Prefix;
    
    public NotificationsService(ISwiftlyCore core, ILogger<NotificationsService> logger, IOptionsMonitor<Configuration> config)
    {
        _core = core;
        _logger = logger;
        core.Registrator.Register(this);

        _config = config.CurrentValue.Notifications;
        _Prefix = config.CurrentValue.Prefix;
        config.OnChange(config =>
        {
            _config = config.Notifications;
            _Prefix = config.Prefix;
        });
    }

    public void NotifyEligiblePlayers(SanctionKind kind, IPlayer affectedPlayer, string detection, string details)
    {
        if(!_config.Enabled) return;

        var players = _core.PlayerManager.GetAllValidPlayers();
        foreach(var player in players)
        {
            if(player.IsFakeClient) continue;

            var playerSteamID = player.SteamID;
            var playerName = player.Name;
            var playerLocalizer = _core.Translation.GetPlayerLocalizer(player);
            var affectedName = affectedPlayer.Name;

            if(_config.Chat.Enabled)
            {
                if(!_core.Permission.PlayerHasPermission(playerSteamID, _config.Chat.Permission))
                {
                    if(_config.DebugLogs) _logger.LogDebug("Player {0}(steamid={1}) does not have permission to receive chat notifications", playerName, playerSteamID);
                    continue;
                }

                string message = kind switch
                {
                    SanctionKind.Ban => playerLocalizer["notifications.chat.ban", affectedName, detection],
                    SanctionKind.Kick => playerLocalizer["notifications.chat.kick", affectedName, detection],
                    SanctionKind.Bypass => playerLocalizer["notifications.chat.bypass", affectedName, detection],
                    _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown sanction kind")
                };

                player.SendChat($"{(_Prefix == "" ? "" : $"{_Prefix} ")}{message}");
                if(_config.DebugLogs) _logger.LogDebug("Player {0}(steamid={1}) has been sent a chat notification.", playerName, playerSteamID);

                if(_core.Permission.PlayerHasPermission(playerSteamID, _config.DetailsPermission))
                {
                    var detailsMessage = playerLocalizer["notifications.chat.details", details];
                    player.SendChat($"{(_Prefix == "" ? "" : $"{_Prefix} ")}{detailsMessage}");
                    
                    if(_config.DebugLogs) _logger.LogDebug("Player {0}(steamid={1}) has been sent a chat details notification.", playerName, playerSteamID);
                }
            }
        }
    }

    public void Dispose()
    {
    }
}