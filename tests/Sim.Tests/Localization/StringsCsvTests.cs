using ChineseGrocery.Sim.Data;

namespace ChineseGrocery.Sim.Tests.Localization;

public class StringsCsvTests
{
    private static readonly IReadOnlyList<string[]> Rows = ReadCsv(RepoPaths.StringsCsv);

    [Fact]
    public void Header_is_keys_en_zh()
    {
        Assert.Equal(["keys", "en", "zh"], Rows[0]);
    }

    [Fact]
    public void Every_row_has_unique_key_and_both_languages()
    {
        var body = Rows.Skip(1).ToList();

        Assert.All(body, r =>
        {
            Assert.Equal(3, r.Length);
            Assert.All(r, cell => Assert.False(string.IsNullOrWhiteSpace(cell), $"empty cell in row '{r[0]}'"));
        });
        Assert.Empty(body.GroupBy(r => r[0]).Where(g => g.Count() > 1).Select(g => g.Key));
    }

    [Fact]
    public void Every_name_key_in_data_has_a_translation()
    {
        var data = GameDataLoader.LoadFromDirectory(RepoPaths.Data);
        var keys = Rows.Skip(1).Select(r => r[0]).ToHashSet();
        var nameKeys = data.Products.Select(p => p.NameKey)
            .Concat(data.Fixtures.Select(f => f.NameKey))
            .Concat(data.CustomerTypes.Select(t => t.NameKey))
            .Concat(data.Employees.Roles.Select(r => r.NameKey))
            .Concat(data.Stages.Select(s => s.NameKey))
            .Concat(data.CustomerTypes.SelectMany(t => t.LineKeys ?? []))
            .Concat(data.Calendar!.Seasons.Select(s => s.NameKey))
            .Concat(data.Festivals!.Festivals.SelectMany(f => (f.LineKeys ?? []).Append(f.NameKey).Append($"festival.{f.Id}.reminder")))
            .Concat(data.Festivals.Decorations.Select(d => d.NameKey))
            .Concat(data.Events!.Events.SelectMany(e => new[] { e.NameKey, $"event.{e.Id}.notice" }))
            .Concat(Enum.GetValues<Department>().Select(d => "ui.department." + System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(d.ToString())))
            .Concat(Enum.GetValues<Sim.Calendar.StoreEventStatus>().Select(s => "ui.store_event.status." + s.ToString().ToLowerInvariant()));

        var missing = nameKeys.Where(k => !keys.Contains(k)).ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void Every_customer_type_has_three_to_five_lines()
    {
        Assert.All(GameDataLoader.LoadFromDirectory(RepoPaths.Data).CustomerTypes, t =>
        {
            Assert.InRange(t.LineKeys?.Count ?? 0, 3, 5);
            Assert.All(t.LineKeys!, k => Assert.Matches("^customer\\.[a-z_]+\\.line\\.[a-z_]+$", k));
        });
    }

    /// <summary>Minimal RFC 4180 reader: quoted fields, doubled quotes, commas and newlines inside quotes.</summary>
    private static List<string[]> ReadCsv(string path)
    {
        var text = File.ReadAllText(path).TrimStart('﻿');
        var rows = new List<string[]>();
        var row = new List<string>();
        var cell = new System.Text.StringBuilder();
        var quoted = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else cell.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { row.Add(cell.ToString()); cell.Clear(); }
            else if (c == '\n')
            {
                row.Add(cell.ToString().TrimEnd('\r'));
                cell.Clear();
                rows.Add([.. row]);
                row.Clear();
            }
            else cell.Append(c);
        }

        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString());
            rows.Add([.. row]);
        }

        return rows;
    }
}
