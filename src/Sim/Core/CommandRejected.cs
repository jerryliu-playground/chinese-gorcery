namespace ChineseGrocery.Sim.Core;

public sealed record CommandRejected(SimCommand Command, RejectReason Reason) : SimEvent;
