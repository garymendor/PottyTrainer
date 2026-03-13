namespace PottyTrainer.Configuration;

public class Character
{
    public string Name { get; set; } = string.Empty;
    public string Server { get; set; } = string.Empty;

    public bool Active { get; set; } = false;

    public int BladderMinimumMinutes { get; set; } = 90;
    public int BladderMaximumMinutes { get; set; } = 180;
    public int BladderAwarenessThresholdMin { get; set; } = 50;
    public int BladderAwarenessThresholdMax { get; set; } = 70;

    public float CurrentBladder { get; set; } = 0;
    public int? CurrentBladderAwarenessThreshold { get; set; } = null;
    public UrgeState CurrentBladderUrgeState { get; set; } = UrgeState.None;

    public int BowelMinimumMinutes { get; set; } = 240;
    public int BowelMaximumMinutes { get; set; } = 480;
    public int BowelAwarenessThresholdMin { get; set; } = 50;
    public int BowelAwarenessThresholdMax { get; set; } = 70;

    public float CurrentBowel { get; set; } = 0;
    public int? CurrentBowelAwarenessThreshold { get; set; } = null;
    public UrgeState CurrentBowelUrgeState { get; set; } = UrgeState.None;

    public const int BladderMin = 30;
    public const int BladderMax = 480;
    public const int BowelMin = 60;
    public const int BowelMax = 3600;
}