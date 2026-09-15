using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using SwiftlyAC.Services.Sanction;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Players;
using TimeSpanParserUtil;

namespace SwiftlyAC.Services.GameEvents;

internal class GameEventsService: IDisposable
{
    private ISwiftlyCore _core {get;init;}
    private SanctionService _sanctionService {get;init;}

    private CancellationTokenSource? _cancellationTokenSource;
    private bool _disposed = false;

    private GameEventsConfiguration _config;
    private ConcurrentDictionary<ulong, DateTime> _lastEventTimes = new();
    private List<string> _detectedGameEvents = [];

    private TimeSpan CheckInterval;
    private TimeSpan DelayInterval;

    public GameEventsService(ISwiftlyCore core, IOptionsMonitor<Configuration> config, SanctionService sanctionService)
    {
        _core = core;
        _sanctionService = sanctionService;

        core.Registrator.Register(this);
        _cancellationTokenSource = core.Scheduler.RepeatBySeconds(1f, CheckTimer);

        _config = config.CurrentValue.GameEvents;
        CheckInterval = TimeSpanParser.TryParse(_config.CheckInterval, out var checkInterval) ? checkInterval : TimeSpan.FromMinutes(1);
        DelayInterval = TimeSpanParser.TryParse(_config.DelayInterval, out var delayInterval) ? delayInterval : TimeSpan.FromSeconds(15);

        config.OnChange(cfg =>
        {
            _config = cfg.GameEvents;
            CheckInterval = TimeSpanParser.TryParse(_config.CheckInterval, out var checkInterval) ? checkInterval : TimeSpan.FromMinutes(1);
            DelayInterval = TimeSpanParser.TryParse(_config.DelayInterval, out var delayInterval) ? delayInterval : TimeSpan.FromSeconds(15);
        });
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _cancellationTokenSource?.Cancel();
        }
    }

    private void CheckTimer()
    {
        if(!_config.Enabled) return;


        var delayIntervalSeconds = (int)DelayInterval.TotalSeconds;

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
        foreach(var gameEvent in _config.BlacklistedGameEvents)
            if(_core.GameEvent.IsListeningToEvent(player.PlayerID, gameEvent))
                _detectedGameEvents.Add(gameEvent);

        if(_detectedGameEvents.Count > 0)
        {
            var detectedEvents = string.Join(", ", _detectedGameEvents);
            _sanctionService.SanctionPlayer(player, SanctionKind.Ban, "Game Events", $"Blacklisted game events: {detectedEvents}");
            _detectedGameEvents.Clear();
        }
    }
}
