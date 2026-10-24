using ChineseGrocery.Sim.Data;
using Godot;

namespace ChineseGrocery.Game;

/// <summary>Boot scene. M0: loads and validates /data so data errors surface on launch.</summary>
public partial class Main : Node2D
{
    public override void _Ready()
    {
        var dataDir = ProjectSettings.GlobalizePath("res://").PathJoin("../data").SimplifyPath();
        try
        {
            var data = GameDataLoader.LoadFromDirectory(dataDir);
            GD.Print($"Data OK: {data.Products.Count} products, {data.Fixtures.Count} fixtures");
        }
        catch (DataValidationException ex)
        {
            GD.PushError(ex.Message);
        }
    }
}
