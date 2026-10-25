using System.Text.Json.Serialization;

namespace ChineseGrocery.Sim.Reputation;

/// <summary>
/// A short customer review written at a day's closing. The view shows the text key review.&lt;TemplateId&gt;.text with
/// {product} filled in from <paramref name="ProductId"/> (if any).
/// </summary>
/// <param name="CustomerTypeId">Who wrote it: a customer type it happened to (null for store-wide facts).</param>
public sealed record Review(int Day, string TemplateId, ReviewFact Fact, int Stars, string? ProductId, string? CustomerTypeId)
{
    [JsonIgnore]
    public string TextKey => $"review.{TemplateId}.text";
}
