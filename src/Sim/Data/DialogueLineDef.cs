namespace ChineseGrocery.Sim.Data;

/// <summary>One line: who says it, its translation key, and the speaker's face.</summary>
public sealed record DialogueLineDef(string Speaker, string Key, PortraitExpression Expression = PortraitExpression.Normal);
