namespace ChineseGrocery.Sim.Calendar;

public enum StoreEventStatus
{
    /// <summary>Known and waiting for its start day, or running.</summary>
    Announced,

    /// <summary>An offer waiting for the player's answer (before opening).</summary>
    Offered,
    Accepted,
    Declined,

    /// <summary>Under way: the inspector is in, or the power is out.</summary>
    Started,
    Ended,
    Passed,
    Failed,
    Delivered,
    Missed,
}
