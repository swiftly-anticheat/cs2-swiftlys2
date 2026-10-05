namespace SwiftlyAC.Services.Strafe;

internal struct AngleFrame
{
    public float Yaw;
    public float FrameTime;
}

internal sealed class StrafeData
{
    public const int MaxFrames = 12;

    public readonly AngleFrame[] Frames = new AngleFrame[MaxFrames];
    public int Start;
    public int Count;
    public float YawAccelPercent;

    // Index 0 is the oldest stored frame.
    public ref AngleFrame this[int index] => ref Frames[(Start + index) % MaxFrames];

    public void Push(float yaw, float frameTime)
    {
        if (Count < MaxFrames)
            Count++;
        else
            Start = (Start + 1) % MaxFrames;
        this[Count - 1] = new AngleFrame { Yaw = yaw, FrameTime = frameTime };
    }
}
