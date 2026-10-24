using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Grid;

namespace ChineseGrocery.Sim.Inventory;

/// <summary>Decides which placed storage area receives a delivered product.</summary>
public static class StorageRouting
{
    /// <summary>The lowest-id placed backroom / cold storage that can hold the product, or null.</summary>
    public static PlacedFixture? FindDestination(GameData data, IEnumerable<PlacedFixture> placed, ProductDef product) =>
        placed
            .OrderBy(p => p.InstanceId)
            .FirstOrDefault(p =>
            {
                var def = data.Fixtures.Single(f => f.Id == p.FixtureId);
                return DataValidator.IsStorageArea(def.Kind) && def.Storage.Contains(product.Storage);
            });
}
