namespace ChineseGrocery.Sim.Data;

public enum TutorialTrigger
{
    /// <summary>Before opening.</summary>
    PreOpen,

    /// <summary>Before opening, once an order has been delivered.</summary>
    GoodsArrived,

    /// <summary>While the store is open.</summary>
    StoreOpen,

    /// <summary>After closing.</summary>
    StoreClosed,

    /// <summary>Before opening, when the next stage's revenue target and cost are both met.</summary>
    UpgradeAvailable,
}
