namespace ChineseGrocery.Sim.Core;

/// <summary>Why a command was refused.</summary>
/// <param name="InstanceId">For layout problems, the fixture involved (e.g. the one whose access is blocked); 0 when none.</param>
public readonly record struct Rejection(RejectReason Reason, int InstanceId = 0);
