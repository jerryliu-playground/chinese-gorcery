namespace ChineseGrocery.Sim.Data;

/// <summary>Someone who speaks in dialogues.</summary>
/// <param name="Portrait">Sprite name prefix of a 64x64 portrait set: the files are &lt;prefix&gt;_normal / _happy / _worried.</param>
/// <param name="Sprite">For characters without portraits: the walking sprite shown in the portrait frame instead.</param>
public sealed record CharacterDef(string Id, string NameKey, string? Portrait = null, string? Sprite = null);
