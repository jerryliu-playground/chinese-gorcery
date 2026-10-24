namespace ChineseGrocery.Sim.Core;

/// <param name="FixtureInstanceId">For layout problems, the fixture involved (e.g. the one whose access would be blocked).</param>
public sealed record CommandRejected(SimCommand Command, RejectReason Reason, int? FixtureInstanceId = null) : SimEvent;
