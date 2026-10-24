using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Inventory;

namespace ChineseGrocery.Sim.Tests.Core;

public class DeterminismTests
{
    private static string Play(ulong seed)
    {
        var sim = SimTestKit.NewSim(seed);
        for (var day = 0; day < 10; day++)
        {
            sim.Enqueue(new PlaceOrder("rice_jasmine", 3 + day));
            sim.Enqueue(new PlaceOrder("soy_milk", 10));
            sim.RunFullDay();
        }

        return SimSnapshot.Describe(sim);
    }

    [Fact]
    public void Same_seed_and_commands_give_identical_results()
    {
        Assert.Equal(Play(1), Play(1));
    }
}
