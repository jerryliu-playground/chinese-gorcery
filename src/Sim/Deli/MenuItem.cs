namespace ChineseGrocery.Sim.Deli;

/// <summary>What a deli station slot sells, and how many portions to cook of it each day.</summary>
public sealed record MenuItem(string DishId, int TargetPortions);
