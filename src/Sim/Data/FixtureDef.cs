using System.Text.Json.Serialization;
using ChineseGrocery.Sim.Grid;

namespace ChineseGrocery.Sim.Data;

/// <summary>Row of fixtures.json.</summary>
/// <param name="InteractionCells">Cells (relative to the unrotated top-left) where the player or staff stand to work it.</param>
/// <param name="CustomerCells">Cells where customers stand to use it (shelf front, checkout queue head, entrance step).</param>
/// <param name="Capacity">Slots: shelves hold one product per slot; storage areas fill slots by each product's unitsPerStorageSlot.</param>
/// <param name="Storage">Storage types this fixture can hold; empty when it holds no goods.</param>
public sealed record FixtureDef(
    string Id,
    string NameKey,
    FixtureKind Kind,
    GridSize Size,
    IReadOnlyList<GridPoint> InteractionCells,
    IReadOnlyList<GridPoint> CustomerCells,
    int Capacity,
    IReadOnlyList<StorageType> Storage,
    long PriceCents,
    int UnlockStage,
    SpriteDef? Sprite = null,
    [property: JsonPropertyName("_tbd")] bool Tbd = false);
