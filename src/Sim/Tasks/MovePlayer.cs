using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Grid;

namespace ChineseGrocery.Sim.Tasks;

/// <summary>Any phase: walk the player to a floor cell along the shortest path. Refused while the player is mid-work.</summary>
public sealed record MovePlayer(GridPoint Target) : SimCommand;
