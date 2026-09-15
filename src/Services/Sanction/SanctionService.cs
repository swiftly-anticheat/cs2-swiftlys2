using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyAC.Services.Notifications;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

namespace SwiftlyAC.Services.Sanction;

internal class SanctionService: IDisposable
{
    private ISwiftlyCore _core;
    private ILogger<SanctionService> _logger;
    private SanctionConfiguration _config;
    private NotificationsService _notificationsService;

    public SanctionService(ISwiftlyCore core, ILogger<SanctionService> logger, IOptionsMonitor<Configuration> config, NotificationsService notificationsService)
    {
        _core = core;
        _logger = logger;
        _notificationsService = notificationsService;

        _config = config.CurrentValue.Sanctions;
        config.OnChange(current => _config = current.Sanctions);

        core.Registrator.Register(this);
    }

    public void Dispose()
    {
    }

    public void SanctionPlayer(IPlayer player, SanctionKind kind, string detection, string details)
    {
        if(_core.Permission.PlayerHasPermission(player.SteamID, _config.BypassPermission))
        {
            kind = SanctionKind.Bypass;
            if(_config.DebugLogs) _logger.LogDebug($"Player {player.Name}(steam_id={player.SteamID}) has bypass permission, skipping sanction.");
        }

        var command = kind switch
        {
            SanctionKind.Ban => _config.BanCommand,
            SanctionKind.Kick => _config.KickCommand,
            _ => null
        };

        _notificationsService.NotifyEligiblePlayers(kind, player, detection, details);
        if(command != null) _core.Engine.ExecuteCommand(command);
    }
}