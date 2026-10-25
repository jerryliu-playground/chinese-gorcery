namespace ChineseGrocery.Sim.Calendar;

/// <summary>What a hygiene inspection would find right now.</summary>
/// <param name="SpoiledGoods">Goods past their last day on display, or dead animals left in a tank.</param>
/// <param name="DirtyWater">A tank's water below the inspector's minimum.</param>
/// <param name="MessyStands">A produce stand below the inspector's minimum tidiness.</param>
public sealed record HygieneFindings(bool SpoiledGoods, bool DirtyWater, bool MessyStands)
{
    public int Problems => (SpoiledGoods ? 1 : 0) + (DirtyWater ? 1 : 0) + (MessyStands ? 1 : 0);

    public bool Passed => Problems == 0;
}
