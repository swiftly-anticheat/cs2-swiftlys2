using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using SwiftlyAC.Services.Sanction;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameHooks;

namespace SwiftlyAC.Services.Strafe;

internal class StrafeService : IDisposable
{
    private const int MaxFrames = 12;

    private struct Frame
    {
        public float Yaw;
        public float FrameTime;
    }

    private sealed class PlayerState
    {
        public readonly Frame[] Frames = new Frame[MaxFrames];
        public int Start;
        public int Count;
        public float YawAccelPercent;

        public ref Frame this[int index] => ref Frames[(Start + index) % MaxFrames];

        public void Push(float yaw, float frameTime)
        {
            if (Count < MaxFrames)
                Count++;
            else
                Start = (Start + 1) % MaxFrames;
            this[Count - 1] = new Frame { Yaw = yaw, FrameTime = frameTime };
        }
    }

    private ISwiftlyCore _core { get; init; }
    private SanctionService _sanctionService { get; init; }
    private StrafeConfiguration _config;
    private bool _disposed = false;

    private readonly ConcurrentDictionary<ulong, PlayerState> _states = new();

    public StrafeService(ISwiftlyCore core, IOptionsMonitor<Configuration> config, SanctionService sanctionService)
    {
        _core = core;
        _sanctionService = sanctionService;

        _config = config.CurrentValue.Strafe;
        config.OnChange(cfg => _config = cfg.Strafe);

        core.Registrator.Register(this);
        _core.GameHooks.Controller.ProcessUsercmds.Pre += OnProcessUsercmdsPre;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _core.GameHooks.Controller.ProcessUsercmds.Pre -= OnProcessUsercmdsPre;
        }
    }

    [EventListener<EventDelegates.OnClientPutInServer>]
    public void OnClientPutInServer(IOnClientPutInServerEvent @ctx)
    {
        var player = _core.PlayerManager.GetPlayer(@ctx.PlayerId);
        if (player == null) return;

        _states.TryRemove(player.SteamID, out _);
    }

    [EventListener<EventDelegates.OnClientDisconnected>]
    public void OnClientDisconnected(IOnClientDisconnectedEvent @ctx)
    {
        var player = _core.PlayerManager.GetPlayer(@ctx.PlayerId);
        if (player == null) return;

        _states.TryRemove(player.SteamID, out _);
    }

    private void OnProcessUsercmdsPre(ref ProcessUsercmdsPreContext ctx)
    {
        if (!_config.Enabled) return;

        var player = ctx.Params.Player;
        if (!player.IsValid || player.IsFakeClient) return;

        if (!_states.TryGetValue(player.SteamID, out var state))
            state = _states.GetOrAdd(player.SteamID, static _ => new PlayerState());

        var frameTime = _core.Engine.GlobalVars.FrameTime;
        var usercmds = ctx.Params.Usercmds;
        for (var i = 0; i < usercmds.Count; i++)
        {
            var yaw = usercmds[i].CSGOUserCmd.Base.Viewangles.Yaw;
            if (DetectOptimization(state, yaw, frameTime))
            {
                _sanctionService.SanctionPlayer(player, SanctionKind.Ban, "Strafe Hack", $"Strafe optimizer detected (yaw accel {state.YawAccelPercent:0.00})");
                state.YawAccelPercent = 0f;
                state.Count = 0;
                return;
            }
        }
    }

    private bool DetectOptimization(PlayerState state, float yaw, float frameTime)
    {
        state.Push(yaw, frameTime);

        var size = state.Count;
        if (size > 3)
        {
            var currentYawSpeed = YawSpeed(state, size - 1);
            var lastYawSpeed = YawSpeed(state, size - 2);
            var switchedStrafeDirection = float.IsNegative(currentYawSpeed) != float.IsNegative(lastYawSpeed);

            var yawAccel2TicksAgo = MathF.Abs(YawAccel(state, size - 3));
            var lastYawAccel = MathF.Abs(YawAccel(state, size - 2));
            var currentYawAccel = MathF.Abs(YawAccel(state, size - 1));

            if (MathF.Abs(currentYawAccel - yawAccel2TicksAgo) < 1.0f)
            {
                var avgAccel = (currentYawAccel + yawAccel2TicksAgo) * 0.5f;
                if (avgAccel < 2.0f && (lastYawAccel - avgAccel) > 2.0f && switchedStrafeDirection)
                    state.YawAccelPercent = state.YawAccelPercent * 0.95f + 0.05f;
                else if (switchedStrafeDirection)
                    state.YawAccelPercent *= 0.95f;
            }
        }

        return state.YawAccelPercent > _config.YawAccelPercentThreshold;
    }

    private static float YawSpeed(PlayerState state, int index)
    {
        if (index == 0) return 0f;

        var diff = state[index].Yaw - state[index - 1].Yaw;
        if (diff > 180.0f) diff -= 360.0f;
        if (diff < -180.0f) diff += 360.0f;
        return diff / state[index].FrameTime;
    }

    private static float YawAccel(PlayerState state, int index)
    {
        if (index < 2) return 0f;
        return (YawSpeed(state, index) - YawSpeed(state, index - 1)) / state[index].FrameTime;
    }
}
