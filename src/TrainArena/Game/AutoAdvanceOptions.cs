namespace TrainArena.Game;

public sealed class AutoAdvanceOptions
{
    public static readonly int[] AllowedDelays = [3, 5, 10];
    public bool Enabled { get; set; }
    public int DelaySeconds { get; set; } = 5;
    public static AutoAdvanceOptions CreateDefault() => new() { Enabled = false, DelaySeconds = 5 };
    public string? Validate() =>
        AllowedDelays.Contains(DelaySeconds) ? null : "AutoAdvanceDelaySeconds muss 3, 5 oder 10 sein.";
}
