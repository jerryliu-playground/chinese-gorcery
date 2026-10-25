using System.Text.Json.Serialization;

namespace ChineseGrocery.Sim.Data;

/// <summary>The protagonist's student loan (spec 18.6). No interest.</summary>
/// <param name="PrincipalCents">Owed at the start of a new game.</param>
/// <param name="MinimumPaymentCents">Taken from cash at closing every <paramref name="PaymentEveryDays"/> days, plus any arrears.</param>
public sealed record LoanDef(
    long PrincipalCents,
    long MinimumPaymentCents,
    int PaymentEveryDays,
    [property: JsonPropertyName("_tbd")] bool Tbd = false);
