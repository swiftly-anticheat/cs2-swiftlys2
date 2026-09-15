using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyAC.Services.Sanction;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Players;
using TimeSpanParserUtil;

namespace SwiftlyAC.Services.ConVars;

internal class ConVarsService: IDisposable
{
    private ISwiftlyCore _core {get;init;}
    private ILogger<ConVarsService> _logger {get;init;}
    private SanctionService _sanctionService {get;init;}
    private ConVarsConfiguration _config;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _disposed = false;

    private ConcurrentDictionary<ulong, DateTime> _lastEventTimes = new();
    private TimeSpan CheckInterval;
    private TimeSpan DelayInterval;

    public ConVarsService(ISwiftlyCore core, ILogger<ConVarsService> logger, IOptionsMonitor<Configuration> config, SanctionService sanctionService)
    {
        _core = core;
        _logger = logger;
        _sanctionService = sanctionService;

        core.Registrator.Register(this);

        _cancellationTokenSource = core.Scheduler.RepeatBySeconds(1f, CheckTimer);
        _config = config.CurrentValue.ConVars;
        CheckInterval = TimeSpanParser.TryParse(_config.CheckInterval, out var checkInterval) ? checkInterval : TimeSpan.FromMinutes(1);
        DelayInterval = TimeSpanParser.TryParse(_config.DelayInterval, out var delayInterval) ? delayInterval : TimeSpan.FromSeconds(15);

        config.OnChange(cfg =>
        {
            _config = cfg.ConVars;
            CheckInterval = TimeSpanParser.TryParse(_config.CheckInterval, out var checkInterval) ? checkInterval : TimeSpan.FromMinutes(1);
            DelayInterval = TimeSpanParser.TryParse(_config.DelayInterval, out var delayInterval) ? delayInterval : TimeSpan.FromSeconds(15);
        });
    }

    private void CheckTimer()
    {
        if(!_config.Enabled) return;

        var delayIntervalSeconds = (int)DelayInterval.TotalSeconds;
        var checkInterval = CheckInterval;

        var players = _core.PlayerManager.GetAllValidPlayers();
        foreach(var player in players)
        {
            if(player.IsFakeClient) continue;
       
            if(!_lastEventTimes.TryGetValue(player.SteamID, out var lastQueryTime))
                if(player.ConnectedTime < delayIntervalSeconds)
                    continue;
                else
                    lastQueryTime = DateTime.UtcNow;


            if(lastQueryTime > DateTime.UtcNow) continue;
            _lastEventTimes[player.SteamID] = DateTime.UtcNow.AddSeconds(Random.Shared.Next(-delayIntervalSeconds, delayIntervalSeconds)).Add(CheckInterval);

            CheckPlayer(player);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _cancellationTokenSource?.Cancel();
        }
    }

    [EventListener<EventDelegates.OnClientPutInServer>]
    public void OnClientPutInServer(IOnClientPutInServerEvent @ctx)
    {
        var player = _core.PlayerManager.GetPlayer(@ctx.PlayerId);
        if(player == null) return;

        _lastEventTimes.TryRemove(player.SteamID, out _);
    }

    [EventListener<EventDelegates.OnClientDisconnected>]
    public void OnClientDisconnected(IOnClientDisconnectedEvent @ctx)
    {
        var player = _core.PlayerManager.GetPlayer(@ctx.PlayerId);
        if(player == null) return;

        _lastEventTimes.TryRemove(player.SteamID, out _);
    }

    public void CheckPlayer(IPlayer player)
    {
        var sensitivityValue = float.TryParse(player.GetClientConvarValue("sensitivity"), CultureInfo.InvariantCulture, out var value) ? value : 0f;
        if(sensitivityValue < 0.0001f || sensitivityValue > 20.0f)
        {
            _sanctionService.SanctionPlayer(player, SanctionKind.Ban, $"ConVar out of Bounds (0.0001 - 20.0)", $"sensitivity: {sensitivityValue}");
            return;
        }
    }
}
