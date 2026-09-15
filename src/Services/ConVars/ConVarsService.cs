using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Options;
using NCalc;
using SwiftlyAC.Services.Sanction;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Convars;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Players;
using TimeSpanParserUtil;

namespace SwiftlyAC.Services.ConVars;

internal class ConVarsService: IDisposable
{
    private ISwiftlyCore _core {get;init;}
    private SanctionService _sanctionService {get;init;}
    private ConVarsConfiguration _config;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _disposed = false;

    private ConcurrentDictionary<ulong, DateTime> _lastEventTimes = new();
    private ConcurrentDictionary<ulong, float> _fpsMaxValues = new();
    private ConcurrentDictionary<string, Expression> _conVarQueryExpressions = new();

    private TimeSpan CheckInterval;
    private TimeSpan DelayInterval;

    private IConVar<bool> _svCheatsConvar;
    private bool _svCheatsValue = false;
    private DateTime _svCheatsLastChanged = DateTime.UtcNow;

    public ConVarsService(ISwiftlyCore core, IOptionsMonitor<Configuration> config, SanctionService sanctionService)
    {
        _core = core;
        _sanctionService = sanctionService;
        _config = null!;

        core.Registrator.Register(this);

        BuildConfig(config.CurrentValue);
        config.OnChange(BuildConfig);

        _cancellationTokenSource = core.Scheduler.RepeatBySeconds(1f, CheckTimer);

        _svCheatsConvar = _core.ConVar.CreateOrFind("sv_cheats", "", false);

        _svCheatsValue = _svCheatsConvar.Value;
        _svCheatsLastChanged = DateTime.UtcNow.AddSeconds(30);
    }

    private void BuildConfig(Configuration config)
    {
        _config = config.ConVars;
        CheckInterval = TimeSpanParser.TryParse(_config.CheckInterval, out var checkInterval) ? checkInterval : TimeSpan.FromMinutes(1);
        DelayInterval = TimeSpanParser.TryParse(_config.DelayInterval, out var delayInterval) ? delayInterval : TimeSpan.FromSeconds(15);

        _conVarQueryExpressions.Clear();
        foreach(var kvp in _config.ConVarQueryExpressions)
        {
            var exp = new Expression(kvp.Value);
            exp.Functions["float"] = (args) => double.TryParse(args.Evaluate(0)?.ToString(), CultureInfo.InvariantCulture, out var parsedValue) ? parsedValue : 0f;
            exp.Functions["int"] = (args) => int.TryParse(args.Evaluate(0)?.ToString(), out var parsedValue) ? parsedValue : 0;
            exp.Functions["bool"] = (args) =>
            {
                var argString = args.Evaluate(0)?.ToString();
                if(argString == null) return false;
                return argString == "1" || argString.Equals("true", StringComparison.CurrentCultureIgnoreCase);  
            };
            _conVarQueryExpressions[kvp.Key] = exp;
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

    [EventListener<EventDelegates.OnConVarValueChanged>]
    private void OnConVarValueChanged(IOnConVarValueChanged @ctx)
    {
        if(@ctx.ConVarName == "sv_cheats")
        {
            _svCheatsValue = bool.TryParse(@ctx.NewValue, out var parsedValue) && parsedValue;
            _svCheatsLastChanged = DateTime.UtcNow.AddSeconds(30);
        }
    }

    private bool ShouldEnforceSvCheats()
    {
        if(_svCheatsValue == true) return false;
        return _svCheatsLastChanged < DateTime.UtcNow;
    }

    public void CheckPlayer(IPlayer player)
    {
        var sensitivityValue = float.TryParse(player.GetClientConvarValue("sensitivity"), CultureInfo.InvariantCulture, out var value) ? value : 0f;
        if(sensitivityValue < 0.0001f || sensitivityValue > 20.0f)
        {
            _sanctionService.SanctionPlayer(player, SanctionKind.Ban, $"ConVar out of Bounds (0.0001 - 20.0)", $"sensitivity: {sensitivityValue}");
            return;
        }

        _core.ConVar.QueryClient(player.PlayerID, "fps_max", (value) =>
        {
            if(!player.IsValid) return;

            var fpsMaxValue = float.TryParse(value, CultureInfo.InvariantCulture, out var parsedValue) ? parsedValue : 0f;
            if(!_fpsMaxValues.TryGetValue(player.SteamID, out var lastFpsMaxValue))
                lastFpsMaxValue = fpsMaxValue;

            if(player.ConnectedTime < 60)
            {
                _fpsMaxValues[player.SteamID] = fpsMaxValue;
                return;
            }

            if(fpsMaxValue - lastFpsMaxValue > 0.1f)
            {
                _sanctionService.SanctionPlayer(player, SanctionKind.Ban, $"ConVar manipulation", $"fps_max: {lastFpsMaxValue} -> {fpsMaxValue}");
                return;
            }
        });

        foreach(var kvp in _conVarQueryExpressions)
        {
            var conVarName = kvp.Key;
            var expression = kvp.Value;

            _core.ConVar.QueryClient(player.PlayerID, conVarName, (value) =>
            {
                if(!player.IsValid) return;

                expression.Parameters["queryValue"] = value;
                expression.Parameters["shouldEnforceSvCheats"] = ShouldEnforceSvCheats();
                
                if(expression.Evaluate() is bool result && result)
                {
                    _sanctionService.SanctionPlayer(player, SanctionKind.Ban, $"ConVar Manipulation", $"{conVarName}: {value}");
                    return;
                }
            });
        }
    }
}
