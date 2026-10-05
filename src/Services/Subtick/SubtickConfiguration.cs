namespace SwiftlyAC.Services.Subtick;

public class SubtickConfiguration
{
    public bool Enabled { get; set; } = true;
    public float InitialIgnoreTime { get; set; } = 10.0f;
    public float InvalidCommandWindow { get; set; } = 5.0f;
    public int InvalidCommandThreshold { get; set; } = 5;
    public float SuspiciousMovesWindow { get; set; } = 0.5f;
    public int SuspiciousMovesThreshold { get; set; } = 20;
    public int YawDeltaThreshold { get; set; } = 2;
    public float SubtickInputsWindow { get; set; } = 20.0f;
    public int SubtickInputsThreshold { get; set; } = 30;
    public float ZeroWhenRatioThreshold { get; set; } = 0.9f;
}
