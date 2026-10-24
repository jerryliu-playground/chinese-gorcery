using ChineseGrocery.Sim.Data;

namespace ChineseGrocery.Sim.Meat;

/// <summary>
/// Splits a primal by weight. Each cut gets its permille of the primal weight, raised by knife skill (the bonus comes out
/// of loss and is shared between cuts in proportion to their base share). Cut weight is boxed by each product's box weight;
/// cut leftovers join the ground-meat share, which is boxed the same way; what remains is loss. The primal's cost is
/// split over the boxes by weight, and the unassigned rest is the loss cost.
/// </summary>
public static class MeatCutting
{
    private const int Whole = 1000;

    public static CutResult Cut(MeatCutDef def, Func<string, ProductDef> products, int knifeSkill, long primalCostCents)
    {
        var primalGrams = products(def.PrimalId).WeightGrams!.Value;
        var baseCuts = def.Cuts.Sum(c => c.Permille);
        var baseLoss = Whole - baseCuts - def.GroundPermille;
        var bonus = Math.Min((long)Math.Max(0, knifeSkill) * def.KnifeSkillCutBonusPermillePerPoint, baseLoss);

        var outputs = new List<CutOutput>();
        long leftoverGrams = 0;
        foreach (var cut in def.Cuts)
        {
            var permille = cut.Permille + bonus * cut.Permille / baseCuts;
            var grams = primalGrams * permille / Whole;
            var box = products(cut.ProductId).WeightGrams!.Value;
            outputs.Add(Boxes(cut.ProductId, grams / box, box));
            leftoverGrams += grams % box;
        }

        var groundGrams = (long)primalGrams * def.GroundPermille / Whole + leftoverGrams;
        var groundBox = products(def.GroundProductId).WeightGrams!.Value;
        outputs.Add(Boxes(def.GroundProductId, groundGrams / groundBox, groundBox));

        var boxedGrams = outputs.Sum(o => (long)o.Packages * products(o.ProductId).WeightGrams!.Value);
        var assignedCost = outputs.Sum(o => o.Packages * o.UnitCostCents);
        return new CutResult(outputs.Where(o => o.Packages > 0).ToList(), (int)(primalGrams - boxedGrams), primalCostCents - assignedCost);

        CutOutput Boxes(string productId, long count, int boxGrams) =>
            new(productId, (int)count, primalCostCents * boxGrams / primalGrams);
    }
}
