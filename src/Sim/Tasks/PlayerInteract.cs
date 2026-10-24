using ChineseGrocery.Sim.Core;

namespace ChineseGrocery.Sim.Tasks;

/// <summary>
/// Any phase: do whatever task the fixture the player stands at offers. <paramref name="FixtureInstanceId"/> picks the
/// fixture being faced when several share the cell; when null, the first fixture with something to do is used.
/// </summary>
public sealed record PlayerInteract(int? FixtureInstanceId = null) : SimCommand;
