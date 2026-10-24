using System.Text;
using System.Text.RegularExpressions;
using ChineseGrocery.Sim.Core;
using ChineseGrocery.Sim.Time;

namespace ChineseGrocery.Sim.Tests.Localization;

/// <summary>Rule 7 for the Godot layer: UI text comes from translation keys, all of which exist in both languages.</summary>
public partial class UiTextTests
{
    private static readonly string Scripts = Path.Combine(RepoPaths.Root, "game", "scripts");

    private static readonly HashSet<string> CsvKeys = File.ReadAllLines(RepoPaths.StringsCsv)
        .Skip(1)
        .Select(line => line.Split(',')[0])
        .ToHashSet();

    [GeneratedRegex("\"(ui\\.[a-z_.]+)\"")]
    private static partial Regex UiKeyLiteral();

    [GeneratedRegex("Text = \"([^\"]*)\"")]
    private static partial Regex TextAssignment();

    private static IEnumerable<string> ScriptFiles() =>
        Directory.EnumerateFiles(Scripts, "*.cs", SearchOption.AllDirectories);

    /// <summary>Same conversion the UI uses for enum-based keys (UiText.Snake).</summary>
    private static string Snake(Enum value)
    {
        var name = value.ToString();
        var sb = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]) && i > 0) sb.Append('_');
            sb.Append(char.ToLowerInvariant(name[i]));
        }

        return sb.ToString();
    }

    [Fact]
    public void Every_ui_key_in_the_game_code_has_a_translation()
    {
        var used = ScriptFiles()
            .SelectMany(f => UiKeyLiteral().Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value))
            .Where(k => !k.EndsWith('.'))
            .ToHashSet();

        var missing = used.Where(k => !CsvKeys.Contains(k)).ToList();

        Assert.NotEmpty(used);
        Assert.Empty(missing);
    }

    [Fact]
    public void Every_rejection_reason_and_phase_has_a_message()
    {
        var keys = Enum.GetValues<RejectReason>().Select(r => "ui.reject." + Snake(r))
            .Concat(Enum.GetValues<DayPhase>().Select(p => "ui.phase." + Snake(p)));

        var missing = keys.Where(k => !CsvKeys.Contains(k)).ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void Ui_code_assigns_no_literal_display_text()
    {
        var literals = ScriptFiles()
            .SelectMany(f => TextAssignment().Matches(File.ReadAllText(f)).Select(m => (File: Path.GetFileName(f), Text: m.Groups[1].Value)))
            .Where(t => t.Text.Length > 0 && !t.Text.StartsWith("ui.", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(literals);
    }

    [Fact]
    public void Game_and_sim_code_contain_no_chinese_text()
    {
        var withCjk = ScriptFiles()
            .Concat(Directory.EnumerateFiles(RepoPaths.SimSource, "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(f => File.ReadAllText(f).Any(c => c is >= '一' and <= '鿿'))
            .Select(Path.GetFileName);

        Assert.Empty(withCjk);
    }
}
