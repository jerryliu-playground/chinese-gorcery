namespace ChineseGrocery.Sim.Data;

/// <summary>products.json "live": seafood kept alive in a fish tank instead of ageing by freshness.</summary>
/// <param name="DeathBasePermille">Chance per animal of dying at a day's close in perfectly clean water.</param>
/// <param name="KillToOrder">Sold at the seafood counter (a worker kills and prepares it) rather than taken by shoppers.</param>
/// <param name="DiesAs">Product a dead animal becomes once it is taken out of the tank; null means it is thrown away.</param>
public sealed record LiveSeafoodDef(int DeathBasePermille, bool KillToOrder = false, string? DiesAs = null);
