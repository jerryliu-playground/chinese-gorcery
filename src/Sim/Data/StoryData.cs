namespace ChineseGrocery.Sim.Data;

/// <summary>
/// The story tables: data/story.json plus every dialogue in data/dialogue/*.json and data/dialogue/side/*.json, in file
/// name order (side stories last). Trigger checks walk <see cref="Dialogues"/> in this order.
/// </summary>
public sealed record StoryData(StorySettingsDef Settings, IReadOnlyList<DialogueDef> Dialogues)
{
    public DialogueDef Dialogue(string id) => Dialogues.First(d => d.Id == id);

    public DialogueDef? FindDialogue(string id) => Dialogues.FirstOrDefault(d => d.Id == id);
}
