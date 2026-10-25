namespace ChineseGrocery.Sim.Economy;

public enum ExpenseCategory
{
    Purchases,
    Wages,
    Fixtures,

    /// <summary>Student loan payments (spec 18.6).</summary>
    Loan,
    /// <summary>Fines, generators and the like; event rewards come back as refunds.</summary>
    Events,
}
