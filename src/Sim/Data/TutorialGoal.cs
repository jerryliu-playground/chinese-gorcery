namespace ChineseGrocery.Sim.Data;

public enum TutorialGoal
{
    PlaceOrder,
    ShelveGoods,
    Checkout,

    /// <summary>The view reports that the daily report was looked at (<c>ReportViewed</c>).</summary>
    ViewReport,
    ChangePrice,

    /// <summary>Upgrade the store, or close the call (the upgrade can wait).</summary>
    UpgradeOrDismiss,
}
