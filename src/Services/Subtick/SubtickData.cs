namespace SwiftlyAC.Services.Subtick;

internal sealed class SubtickData
{
    public readonly Queue<double> InvalidCommands = new();
    public readonly Queue<double> SuspiciousMoves = new();
    public readonly Queue<double> CommandsWithSubtick = new();
    public readonly Queue<double> ZeroWhenCommands = new();
}
