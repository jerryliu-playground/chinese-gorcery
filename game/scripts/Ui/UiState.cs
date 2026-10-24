using ChineseGrocery.Sim.Grid;

namespace ChineseGrocery.Game.Ui;

public enum ToolMode
{
    /// <summary>Clicks walk the player; clicking a shelf opens its slots.</summary>
    Play,

    /// <summary>Clicks select fixtures (to move, turn or sell) or place the chosen new one.</summary>
    Build,
}

/// <summary>What the player is doing with the mouse; shared by the store view and the panels.</summary>
public sealed class UiState
{
    public ToolMode Mode { get; set; } = ToolMode.Play;

    /// <summary>In build mode: the fixture definition being placed, if any.</summary>
    public string? PlacingFixtureId { get; set; }

    /// <summary>In build mode: the placed fixture being moved, if any.</summary>
    public int? MovingInstanceId { get; set; }

    public Rotation GhostRotation { get; set; } = Rotation.None;

    /// <summary>The placed fixture the panels are showing.</summary>
    public int? SelectedInstanceId { get; set; }

    public event Action? Changed;

    public void Notify() => Changed?.Invoke();

    public void ClearTool()
    {
        PlacingFixtureId = null;
        MovingInstanceId = null;
        GhostRotation = Rotation.None;
    }
}
