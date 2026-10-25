using Godot;

namespace ChineseGrocery.Game.View;

/// <summary>
/// Hand-feel tuning for animations, pop-ups and bubbles, edited in game/config/feel.tres. Times are real seconds at 1x
/// speed; effects run faster at higher speeds.
/// </summary>
[GlobalClass]
public partial class FeelConfig : Resource
{
    public const string Path = "res://config/feel.tres";

    [ExportGroup("Walking")]
    [Export] public float WalkBounceHeight { get; set; } = 2f;
    [Export] public float WalkBounceSeconds { get; set; } = 0.25f;

    [ExportGroup("Money")]
    [Export] public float FloatTextSeconds { get; set; } = 1.0f;
    [Export] public float FloatTextRise { get; set; } = 18f;
    [Export] public float CashRollSeconds { get; set; } = 0.6f;

    [ExportGroup("Shelves and fixtures")]
    [Export] public float ShelfBounceSeconds { get; set; } = 0.15f;
    [Export] public float ShelfBounceSquash { get; set; } = 0.12f;
    [Export] public float ShelfFlashSeconds { get; set; } = 0.2f;
    [Export] public float PlaceBounceSeconds { get; set; } = 0.25f;
    [Export] public float ChopSeconds { get; set; } = 0.4f;
    [Export] public float CutPopSeconds { get; set; } = 0.8f;

    [ExportGroup("Produce stands")]
    /// <summary>Stands at or above this display score sparkle now and then.</summary>
    [Export(PropertyHint.Range, "0,100,1")] public int SparkleScore { get; set; } = 80;
    [Export] public float SparklePeriodSeconds { get; set; } = 3.0f;
    [Export] public float SparkleSeconds { get; set; } = 0.4f;
    /// <summary>How far (pixels) produce shifts in its crate when the stand is messy.</summary>
    [Export] public int MessJitter { get; set; } = 1;

    [ExportGroup("Staff")]
    /// <summary>How often staff show a morale icon, and for how long.</summary>
    [Export] public float MoraleIconPeriodSeconds { get; set; } = 8f;
    [Export] public float MoraleIconSeconds { get; set; } = 1.5f;

    [ExportGroup("Customers")]
    [Export] public float BubbleSeconds { get; set; } = 1.2f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float HeartChance { get; set; } = 0.3f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float SpeechChancePerGameMinute { get; set; } = 0.01f;
    [Export] public float SpeechSeconds { get; set; } = 2.0f;

    [ExportGroup("Celebration")]
    [Export] public float CelebrationSeconds { get; set; } = 2.0f;
    [Export] public int ConfettiCount { get; set; } = 80;

    [ExportGroup("Dialogue")]
    [Export] public float DialogueCharsPerSecond { get; set; } = 30f;
    /// <summary>How long the phone icon rings hard at the start of a call.</summary>
    [Export] public float PhoneRingSeconds { get; set; } = 0.8f;

    [ExportGroup("Speed")]
    /// <summary>Above this speed multiplier, minor effects (hearts, speech, shelf flashes) are skipped.</summary>
    [Export] public int MinorEffectsMaxSpeed { get; set; } = 2;

    public static FeelConfig Load() => ResourceLoader.Exists(Path) ? GD.Load<FeelConfig>(Path) : new FeelConfig();
}
