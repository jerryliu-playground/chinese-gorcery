namespace ChineseGrocery.Sim.Reputation;

public enum GroupOrderStatus
{
    /// <summary>Proposed by a buyer, waiting for the player to accept or decline (until the next day starts).</summary>
    Offered,

    /// <summary>Accepted: goods are set aside from storage until it is complete or due.</summary>
    Active,
}
