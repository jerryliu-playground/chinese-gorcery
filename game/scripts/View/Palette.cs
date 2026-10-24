using ChineseGrocery.Sim.Customers;
using ChineseGrocery.Sim.Data;
using Godot;

namespace ChineseGrocery.Game.View;

/// <summary>Placeholder art: flat colours per fixture kind, department and actor.</summary>
public static class Palette
{
    public static readonly Color Background = new(0.10f, 0.11f, 0.13f);
    public static readonly Color FloorA = new(0.80f, 0.76f, 0.66f);
    public static readonly Color FloorB = new(0.76f, 0.72f, 0.62f);
    public static readonly Color Outline = new(0.15f, 0.12f, 0.10f);
    public static readonly Color Player = new(0.20f, 0.45f, 0.90f);
    public static readonly Color Staff = new(0.95f, 0.55f, 0.15f);
    public static readonly Color WorkCell = new(1f, 1f, 1f, 0.25f);
    public static readonly Color CustomerCell = new(0.3f, 0.9f, 0.4f, 0.25f);
    public static readonly Color GhostOk = new(0.3f, 0.9f, 0.3f, 0.55f);
    public static readonly Color GhostBad = new(0.95f, 0.25f, 0.25f, 0.55f);
    public static readonly Color Selection = new(1f, 0.95f, 0.3f);
    public static readonly Color Progress = new(0.3f, 0.95f, 0.4f);

    public static Color Fixture(FixtureKind kind) => kind switch
    {
        FixtureKind.Shelf => new Color(0.55f, 0.38f, 0.22f),
        FixtureKind.Checkout => new Color(0.40f, 0.48f, 0.58f),
        FixtureKind.Backroom => new Color(0.36f, 0.26f, 0.18f),
        FixtureKind.Entrance => new Color(0.25f, 0.62f, 0.32f),
        FixtureKind.MeatDisplay => new Color(0.62f, 0.80f, 0.90f),
        FixtureKind.CuttingTable => new Color(0.58f, 0.60f, 0.64f),
        FixtureKind.ColdStorage => new Color(0.20f, 0.30f, 0.55f),
        _ => Colors.Magenta,
    };

    public static Color Department(Department department) => department switch
    {
        Sim.Data.Department.DryGoods => new Color(0.93f, 0.85f, 0.55f),
        Sim.Data.Department.Sauces => new Color(0.60f, 0.15f, 0.12f),
        Sim.Data.Department.Snacks => new Color(0.98f, 0.70f, 0.20f),
        Sim.Data.Department.Drinks => new Color(0.35f, 0.80f, 0.85f),
        Sim.Data.Department.Meat => new Color(0.92f, 0.45f, 0.50f),
        _ => Colors.Magenta,
    };

    public static Color Customer(int typeIndex, CustomerState state)
    {
        Color[] types = [new(0.75f, 0.35f, 0.75f), new(0.30f, 0.70f, 0.95f), new(0.95f, 0.85f, 0.30f)];
        var c = types[typeIndex % types.Length];
        return state == CustomerState.Leaving ? c.Darkened(0.4f) : c;
    }
}
