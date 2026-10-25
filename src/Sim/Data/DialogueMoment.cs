namespace ChineseGrocery.Sim.Data;

/// <summary>Points in play where dialogue triggers are checked.</summary>
public enum DialogueMoment
{
    NewGame,

    /// <summary>Each morning from day 2 on (day 1 starts with the opening instead).</summary>
    DayStart,
    StoreOpen,
    StoreClose,
    StageReached,
    LoanPaidOff,

    /// <summary>A scheduled loan payment could not be paid in full.</summary>
    LoanShort,
}
