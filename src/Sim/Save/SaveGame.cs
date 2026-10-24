using System.Text.Json;
using System.Text.Json.Serialization;
using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Data;

namespace ChineseGrocery.Sim.Save;

/// <summary>Writes and reads saves as JSON. Bump <see cref="CurrentVersion"/> when the format changes and migrate old files in <see cref="Read"/>.</summary>
public static class SaveGame
{
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false) },
    };

    public static string Write(GameSim sim) => JsonSerializer.Serialize(sim.ToSave(), Options);

    /// <summary>Rebuilds a game from a save, against the given (current) data.</summary>
    public static GameSim Read(GameData data, string json)
    {
        SaveFile save;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("version", out var v) || v.ValueKind != JsonValueKind.Number)
            {
                throw new SaveException("Save has no version number.");
            }

            if (v.GetInt32() != CurrentVersion) throw new SaveException($"Save version {v.GetInt32()} is not supported (expected {CurrentVersion}).");

            save = JsonSerializer.Deserialize<SaveFile>(json, Options) ?? throw new SaveException("Save is empty.");
        }
        catch (JsonException ex)
        {
            throw new SaveException($"Save is not valid: {ex.Message}", ex);
        }

        try
        {
            return GameSim.FromSave(data, save);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or ArgumentException)
        {
            throw new SaveException($"Save does not match the game data: {ex.Message}", ex);
        }
    }
}
