namespace ChineseGrocery.Sim.Data;

/// <summary>
/// Reads data/story.json and the dialogue files (data/dialogue/*.json, then data/dialogue/side/*.json, each folder in
/// file name order), then validates them against the rest of the data. Problems throw <see cref="DataValidationException"/>.
/// </summary>
public static class StoryLoader
{
    public const string SettingsFile = "story.json";
    public const string DialogueFolder = "dialogue";
    public const string SideFolder = "dialogue/side";

    public static StoryData LoadFromDirectory(string dataDirectory, GameData data)
    {
        if (!File.Exists(Path.Combine(dataDirectory, SettingsFile)))
        {
            throw new DataValidationException([$"{SettingsFile}: file not found in '{dataDirectory}'"]);
        }

        return Load(file => File.ReadAllText(Path.Combine(dataDirectory, file)), DialogueFiles(dataDirectory), data);
    }

    /// <summary>Dialogue files under <paramref name="dataDirectory"/>, relative and with '/' separators, main folder first.</summary>
    public static IReadOnlyList<string> DialogueFiles(string dataDirectory)
    {
        IEnumerable<string> In(string folder)
        {
            var dir = Path.Combine(dataDirectory, folder);
            return Directory.Exists(dir)
                ? Directory.EnumerateFiles(dir, "*.json").Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal).Select(f => folder + "/" + f)
                : [];
        }

        return [.. In(DialogueFolder), .. In(SideFolder)];
    }

    /// <summary>Loads through <paramref name="readFile"/> (file name relative to the data folder -> JSON text).</summary>
    public static StoryData Load(Func<string, string> readFile, IReadOnlyList<string> dialogueFiles, GameData data)
    {
        var errors = new List<string>();
        StorySettingsDef? settings = null;
        try
        {
            settings = GameDataLoader.Deserialize<StorySettingsDef>(SettingsFile, readFile(SettingsFile));
        }
        catch (DataValidationException ex)
        {
            errors.AddRange(ex.Errors);
        }

        var dialogues = new List<DialogueDef>();
        foreach (var file in dialogueFiles)
        {
            try
            {
                var defs = GameDataLoader.Deserialize<List<DialogueDef>>(file, readFile(file));
                if (file.StartsWith(SideFolder + "/", StringComparison.Ordinal))
                {
                    errors.AddRange(defs.Where(d => !d.Optional).Select(d => $"{file}: side story '{d.Id}' must be optional"));
                }

                dialogues.AddRange(defs);
            }
            catch (DataValidationException ex)
            {
                errors.AddRange(ex.Errors);
            }
        }

        if (errors.Count > 0) throw new DataValidationException(errors);

        var story = new StoryData(settings!, dialogues);
        var problems = StoryValidator.Validate(story, data);
        if (problems.Count > 0) throw new DataValidationException(problems);
        return story;
    }
}
