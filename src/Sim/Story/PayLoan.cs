namespace ChineseGrocery.Sim.Story;

/// <summary>A voluntary extra payment on the student loan (capped at what is left).</summary>
public sealed record PayLoan(long AmountCents) : StoryCommand;
