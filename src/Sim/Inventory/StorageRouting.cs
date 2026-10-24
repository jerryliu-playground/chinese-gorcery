using ChineseGrocery.Sim.Data;

namespace ChineseGrocery.Sim.Inventory;

/// <summary>Decides which storage area receives a delivered product.</summary>
public static class StorageRouting
{
    /// <summary>First storage-area fixture unlocked at <paramref name="stage"/> that can hold the product, or null.</summary>
    public static FixtureDef? FindDestination(GameData data, ProductDef product, int stage) =>
        data.Fixtures.FirstOrDefault(f =>
            DataValidator.IsStorageArea(f.Kind) && f.UnlockStage <= stage && f.Storage.Contains(product.Storage));
}
