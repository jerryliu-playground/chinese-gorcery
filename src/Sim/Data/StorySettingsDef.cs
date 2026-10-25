using System.Text.Json.Serialization;

namespace ChineseGrocery.Sim.Data;

/// <summary>Root of story.json.</summary>
/// <param name="TutorialStartsAfter">Dialogue that must be shown (finished or skipped) before the first tutorial call: the end of the opening.</param>
/// <param name="ShopNameMaxLength">Longest shop name the player may enter, in characters.</param>
/// <param name="DefaultShopNameKey">Translation key of the shop's name until the player names it.</param>
public sealed record StorySettingsDef(
    IReadOnlyList<CharacterDef> Characters,
    string TutorialStartsAfter,
    IReadOnlyList<TutorialStepDef> Tutorial,
    LoanDef Loan,
    CompetitionDef Competition,
    int ShopNameMaxLength,
    string DefaultShopNameKey,
    [property: JsonPropertyName("_tbd")] bool Tbd = false);
