namespace ChineseGrocery.Sim.Reputation;

/// <summary>Things that happen during a day that a review can be about.</summary>
public enum ReviewFact
{
    /// <summary>A product on display ran out (product).</summary>
    SoldOut,

    /// <summary>A customer could not find a product (product).</summary>
    NotFound,

    /// <summary>A customer thought a product too expensive (product).</summary>
    TooExpensive,

    /// <summary>A paying customer waited a long time in a line.</summary>
    LongQueue,

    /// <summary>A customer gave up waiting and left.</summary>
    GaveUp,

    /// <summary>Enough customers paid and nobody waited long or gave up.</summary>
    QuickCheckout,

    /// <summary>A customer bought fresh produce, meat or seafood (product).</summary>
    FreshBuy,

    /// <summary>A customer bought a dish from the deli (product).</summary>
    DeliTreat,

    /// <summary>A trend seeker found the trending product (product).</summary>
    TrendFound,

    /// <summary>A trend seeker could not find the trending product (product).</summary>
    TrendMissed,

    /// <summary>A customer wanting English labels was confused without them.</summary>
    LabelsConfused,

    /// <summary>A customer wanting English labels shopped with them.</summary>
    LabelsLoved,

    /// <summary>A restaurant group order was delivered on time.</summary>
    GroupOrderDelivered,

    /// <summary>A restaurant group order was late or short and was cancelled.</summary>
    GroupOrderFailed,
}
