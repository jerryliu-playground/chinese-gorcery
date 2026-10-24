using System.Text.Json.Serialization;
using ChineseGrocery.Sim.Grid;

namespace ChineseGrocery.Sim.Data;

/// <summary>Row of fixtures.json.</summary>
/// <param name="InteractionCells">Cells (relative to the fixture's top-left) where an actor stands to use it.</param>
/// <param name="Storage">Storage types this fixture can hold; empty when it holds no goods.</param>
public sealed record FixtureDef(
    string Id,
    string NameKey,
    FixtureKind Kind,
    GridSize Size,
    IReadOnlyList<GridPoint> InteractionCells,
    int Capacity,
    IReadOnlyList<StorageType> Storage,
    long PriceCents,
    int UnlockStage,
    [property: JsonPropertyName("_tbd")] bool Tbd = false);
