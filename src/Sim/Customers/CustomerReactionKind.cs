namespace ChineseGrocery.Sim.Customers;

public enum CustomerReactionKind
{
    NotFound,
    TooExpensive,
    TookItem,
    GaveUp,

    /// <summary>Found what they love (a trending product): a heart.</summary>
    Heart,

    /// <summary>Puzzled by labels they cannot read: a question mark.</summary>
    Confused,
}
