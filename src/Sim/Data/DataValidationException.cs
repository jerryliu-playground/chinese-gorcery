namespace ChineseGrocery.Sim.Data;

/// <summary>Thrown when data files are malformed or inconsistent. Messages are developer diagnostics, not player-facing text.</summary>
public sealed class DataValidationException(IReadOnlyList<string> errors)
    : Exception("Invalid game data:" + Environment.NewLine + string.Join(Environment.NewLine, errors.Select(e => "  - " + e)))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
