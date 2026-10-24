namespace ChineseGrocery.Sim.Core;

/// <summary>A request from the view layer. Queued, then applied at the next tick boundary.</summary>
public abstract record SimCommand;
