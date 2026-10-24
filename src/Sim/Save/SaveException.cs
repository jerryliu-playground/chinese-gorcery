namespace ChineseGrocery.Sim.Save;

/// <summary>A save that cannot be loaded (corrupt, incomplete, or from an unknown version). Developer-facing message.</summary>
public sealed class SaveException(string message, Exception? inner = null) : Exception(message, inner);
