using System.Text.Json.Serialization;

namespace ChineseGrocery.Sim.Data;

/// <summary>Row of meat_cuts.json: how one primal splits into cuts, ground meat and loss.</summary>
/// <param name="GroundPermille">Share of the primal that becomes ground meat; whatever remains after cuts and ground is loss.</param>
/// <param name="KnifeSkillCutBonusPermillePerPoint">Shift from loss to cuts per knife-skill point.</param>
public sealed record MeatCutDef(
    string PrimalId,
    IReadOnlyList<CutYield> Cuts,
    string GroundProductId,
    int GroundPermille,
    int KnifeSkillCutBonusPermillePerPoint,
    [property: JsonPropertyName("_tbd")] bool Tbd = false);

public sealed record CutYield(string ProductId, int Permille);
