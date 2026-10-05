namespace SwiftlyAC.Services.Strafe;

public class StrafeConfiguration
{
    public bool Enabled { get; set; } = true;
    public float YawAccelPercentThreshold { get; set; } = 0.9f;
}
