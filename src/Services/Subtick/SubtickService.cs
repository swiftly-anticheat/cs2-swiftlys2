
using Microsoft.Extensions.Options;
using SwiftlyAC.Services.Sanction;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameHooks;
using SwiftlyS2.Shared.ProtobufDefinitions;
using SwiftlyS2.Shared.Players;

namespace SwiftlyAC.Services.Subtick;

internal class SubtickService : IDisposable
{
    private const int EngineMaxSubtickMoves = 32;
    private const float MaxSubtickMoveImpulse = 2.0f;

    private const ulong InAttack = 1UL << 0;
    private const ulong InJump = 1UL << 1;
    private const ulong InDuck = 1UL << 2;
    private const ulong InForward = 1UL << 3;
    private const ulong InBack = 1UL << 4;
    private const ulong InMoveLeft = 1UL << 9;
    private const ulong InMoveRight = 1UL << 10;
    private const ulong InAttack2 = 1UL << 11;
    private const ulong MoveButtonMask = InAttack | InAttack2 | InJump | InDuck | InForward | InBack | InMoveLeft | InMoveRight;

    private struct Step
    {
        public ulong Button;
        public bool Pressed;
        public float When;
        public float AnalogForward;
        public float AnalogLeft;
        public float YawDelta;
    }

    private struct Impulses
    {
        public bool Loaded;
        public float Forward;
        public float Side;
    }

    private ISwiftlyCore _core { get; init; }
    private SanctionService _sanctionService { get; init; }
    private SubtickConfiguration _config;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _disposed = false;

    public SubtickService(ISwiftlyCore core, IOptionsMonitor<Configuration> config, SanctionService sanctionService)
    {
        _core = core;
        _sanctionService = sanctionService;

        _config = config.CurrentValue.Subtick;
        config.OnChange(cfg => _config = cfg.Subtick);

        core.Registrator.Register(this);
        _core.GameHooks.Controller.ProcessUsercmds.Pre += OnProcessUsercmdsPre;
        _cancellationTokenSource = core.Scheduler.RepeatBySeconds(1f, CheckTimer);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _core.GameHooks.Controller.ProcessUsercmds.Pre -= OnProcessUsercmdsPre;
            _cancellationTokenSource?.Cancel();
        }
    }

    private static double Now => Environment.TickCount64 / 1000.0;

    private void OnProcessUsercmdsPre(ref ProcessUsercmdsPreContext ctx)
    {
        if (!_config.Enabled) return;

        var player = ctx.Params.Player;
        if (!player.IsValid || player.IsFakeClient) return;

        var state = player.AcData.Subtick;

        var impulses = new Impulses();
        var usercmds = ctx.Params.Usercmds;
        for (var i = 0; i < usercmds.Count; i++)
            CheckCommand(state, player, usercmds[i].CSGOUserCmd, ref impulses);
    }

    private void CheckCommand(SubtickData state, IPlayer player, CSGOUserCmdPB userCmd, ref Impulses impulses)
    {
        var baseCmd = userCmd.Base;
        var moves = baseCmd.SubtickMoves;
        var count = moves.Count;
        // ponytail: reads at most 33 moves; more than 32 is already engine-rejected, 33 is enough to tell.
        var n = Math.Min(count, EngineMaxSubtickMoves + 1);

        var buttons = baseCmd.ButtonsPb;
        var expected = ((ulong)buttons.Buttonstate2 | (ulong)buttons.Buttonstate3) & (InJump | InDuck);

        if (n == 0)
        {
            if (expected != 0)
            {
                var now = Now;
                lock (state) state.InvalidCommands.Enqueue(now);
            }
            return;
        }

        Span<Step> steps = stackalloc Step[EngineMaxSubtickMoves + 1];
        for (var i = 0; i < n; i++)
        {
            var s = moves.Get(i);
            steps[i] = new Step
            {
                Button = s.Button,
                Pressed = s.Pressed,
                When = s.When,
                AnalogForward = s.AnalogForwardDelta,
                AnalogLeft = s.AnalogLeftDelta,
                YawDelta = s.YawDelta,
            };
        }
        var span = steps[..n];

        // Every pressed/released jump/duck in the buttonstate must be accounted for in a subtick move.
        foreach (ref readonly var s in span)
            expected &= ~s.Button;
        var invalid = expected != 0;

        if (!impulses.Loaded)
        {
            var li = player.PlayerPawn?.MovementServices?.LastMovementImpulses;
            impulses = new Impulses { Loaded = true, Forward = li?.X ?? 0f, Side = li?.Y ?? 0f };
        }

        var excessiveYaw = HasExcessiveYawMoves(span);
        var zeroWhen = PossibleDesubtickedCommand(span, count, impulses.Forward, impulses.Side);

        var t = Now;
        lock (state)
        {
            if (invalid) state.InvalidCommands.Enqueue(t);
            if (excessiveYaw) state.SuspiciousMoves.Enqueue(t);
            state.CommandsWithSubtick.Enqueue(t);
            if (zeroWhen) state.ZeroWhenCommands.Enqueue(t);
        }
    }

    // Yaw alias abuse: more than YawDeltaThreshold moves carrying a yaw change in one tick.
    private bool HasExcessiveYawMoves(ReadOnlySpan<Step> steps)
    {
        var count = 0;
        foreach (ref readonly var s in steps)
        {
            if (s.YawDelta == 0.0f) continue;
            if (++count > _config.YawDeltaThreshold) return true;
        }
        return false;
    }

    // The engine drops the entire move list when any move fails validation. True only when the
    // list passes every check up to the point an axis exceeds the impulse limit.
    private static bool ExceedsImpulseLimit(ReadOnlySpan<Step> steps, int totalCount, float forwardAxis, float sideAxis)
    {
        if (totalCount > EngineMaxSubtickMoves) return false;

        var lastWhen = 0f;
        foreach (ref readonly var s in steps)
        {
            if (s.When < lastWhen || s.When > 1.0f) return false;
            lastWhen = s.When;

            var button = s.Button;
            if (button != 0)
            {
                if ((button & (button - 1)) != 0 || (button & MoveButtonMask) == 0) return false;

                var delta = s.Pressed ? 1.0f : -1.0f;
                if (button == InForward) forwardAxis += delta;
                else if (button == InBack) forwardAxis -= delta;
                else if (button == InMoveLeft) sideAxis += delta;
                else if (button == InMoveRight) sideAxis -= delta;
            }
            else
            {
                forwardAxis += s.AnalogForward;
                sideAxis += s.AnalogLeft;
            }

            if (MathF.Abs(forwardAxis) > MaxSubtickMoveImpulse || MathF.Abs(sideAxis) > MaxSubtickMoveImpulse)
                return true;
        }
        return false;
    }

    private static bool PossibleDesubtickedCommand(ReadOnlySpan<Step> steps, int totalCount, float forward, float side)
    {
        // `forwardback 5 0 0` makes the engine discard the list and fall back to buttonstates alone.
        if (ExceedsImpulseLimit(steps, totalCount, forward, side)) return true;

        var hasZeroWhen = true;
        var hasNonAnalogMove = false;
        foreach (ref readonly var s in steps)
        {
            // Controller-issued analog moves legitimately have no 'when'.
            if (s.AnalogForward != 0f || s.AnalogLeft != 0f) continue;
            hasNonAnalogMove = true;
            hasZeroWhen &= s.When == 0.0f;
        }
        return hasNonAnalogMove && hasZeroWhen;
    }

    private static void Prune(Queue<double> q, double now, float window)
    {
        while (q.Count > 0 && now - q.Peek() > window) q.Dequeue();
    }

    private void CheckTimer()
    {
        if (!_config.Enabled) return;

        var now = Now;
        foreach (var player in _core.PlayerManager.GetAllValidPlayers())
        {
            if (player.IsFakeClient) continue;
            if (player.ConnectedTime < _config.InitialIgnoreTime) continue;
            var state = player.AcData.Subtick;

            lock (state) CheckPlayer(player, state, now);
        }
    }

    private void CheckPlayer(IPlayer player, SubtickData state, double now)
    {
        Prune(state.InvalidCommands, now, _config.InvalidCommandWindow);
        if (state.InvalidCommands.Count >= _config.InvalidCommandThreshold)
        {
            _sanctionService.SanctionPlayer(player, SanctionKind.Ban, "Invalid Input", $"Excessive invalid commands detected: {state.InvalidCommands.Count}");
            state.InvalidCommands.Clear();
        }

        Prune(state.SuspiciousMoves, now, _config.SuspiciousMovesWindow);
        if (state.SuspiciousMoves.Count >= _config.SuspiciousMovesThreshold)
        {
            _sanctionService.SanctionPlayer(player, SanctionKind.Ban, "Subtick Spam", $"Excessive subtick moves detected: {state.SuspiciousMoves.Count}");
            state.SuspiciousMoves.Clear();
        }

        Prune(state.CommandsWithSubtick, now, _config.SubtickInputsWindow);
        Prune(state.ZeroWhenCommands, now, _config.SubtickInputsWindow);
        if (state.CommandsWithSubtick.Count >= _config.SubtickInputsThreshold)
        {
            var ratio = (float)state.ZeroWhenCommands.Count / state.CommandsWithSubtick.Count;
            if (ratio >= _config.ZeroWhenRatioThreshold)
            {
                _sanctionService.SanctionPlayer(player, SanctionKind.Ban, "Desubtick", $"Desubticking detected (ratio {ratio:0.00})");
                state.ZeroWhenCommands.Clear();
                state.CommandsWithSubtick.Clear();
            }
        }
    }
}
