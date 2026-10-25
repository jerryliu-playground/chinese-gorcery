namespace ChineseGrocery.Sim.Economy;

public enum ExpenseCategory
{
    Purchases,
    Wages,
    Fixtures,

    /// <summary>Fines, generators and the like; event rewards come back as refunds.</summary>
    Events,
}
