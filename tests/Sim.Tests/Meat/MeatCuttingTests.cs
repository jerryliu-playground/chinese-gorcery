using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Meat;

namespace ChineseGrocery.Sim.Tests.Meat;

public class MeatCuttingTests
{
    private static readonly MeatCutDef Pork = SimTestKit.Data.MeatCuts.Single(c => c.PrimalId == "pork_primal");
    private static ProductDef Product(string id) => SimTestKit.Data.Products.Single(p => p.Id == id);
    private static int Grams(string id) => Product(id).WeightGrams!.Value;

    private static CutResult Cut(int skill, long cost = 6000, MeatCutDef? def = null) => MeatCutting.Cut(def ?? Pork, Product, skill, cost);

    [Fact]
    public void Worked_example_with_no_knife_skill()
    {
        // 20 kg primal, 500 g boxes: belly 25% = 10 boxes, ribs 20% = 8, loin 20% = 8, ground 25% = 10, loss 10% = 2 kg.
        var def = Pork with
        {
            Cuts = [new CutYield("pork_belly", 250), new CutYield("pork_ribs", 200), new CutYield("pork_loin", 200)],
            GroundPermille = 250,
        };
        var primal = Product("pork_primal") with { WeightGrams = 20000 };
        ProductDef Lookup(string id) => id == "pork_primal" ? primal : Product(id) with { WeightGrams = 500 };

        var result = MeatCutting.Cut(def, Lookup, knifeSkill: 0, primalCostCents: 6000);

        Assert.Equal(
            [new CutOutput("pork_belly", 10, 150), new CutOutput("pork_ribs", 8, 150), new CutOutput("pork_loin", 8, 150), new CutOutput("ground_pork", 10, 150)],
            result.Outputs);
        Assert.Equal(2000, result.LossGrams);
        Assert.Equal(600, result.LossCents);
    }

    [Fact]
    public void Weight_and_cost_are_conserved()
    {
        foreach (var skill in Enumerable.Range(0, 20))
        {
            var r = Cut(skill, cost: 6001);

            Assert.Equal(Grams("pork_primal"), r.Outputs.Sum(o => o.Packages * Grams(o.ProductId)) + r.LossGrams);
            Assert.Equal(6001, r.Outputs.Sum(o => o.Packages * o.UnitCostCents) + r.LossCents);
            Assert.True(r.LossGrams >= 0 && r.LossCents >= 0);
        }
    }

    [Fact]
    public void Better_knife_skill_never_yields_less_and_never_loses_more()
    {
        var results = Enumerable.Range(0, 15).Select(skill => Cut(skill)).ToList();

        foreach (var (worse, better) in results.Zip(results.Skip(1)))
        {
            Assert.True(better.LossGrams <= worse.LossGrams);
            Assert.True(better.Outputs.Sum(o => o.Packages) >= worse.Outputs.Sum(o => o.Packages));
        }

        Assert.True(results[^1].LossGrams < results[0].LossGrams, "skill should matter over the range");
    }

    [Fact]
    public void Knife_skill_bonus_is_capped_by_the_base_loss()
    {
        Assert.Equal(Cut(1000).Outputs, Cut(100_000).Outputs);
    }
}
