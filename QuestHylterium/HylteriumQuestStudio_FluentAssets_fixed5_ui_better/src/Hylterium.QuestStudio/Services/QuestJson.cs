using System.Text.Json;
using Hylterium.QuestStudio.Models;

namespace Hylterium.QuestStudio.Services;

public static class QuestJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static QuestBundle LoadFromFile(string path)
    {
        var json = File.ReadAllText(path);
        var bundle = JsonSerializer.Deserialize<QuestBundle>(json, Options);
        return bundle ?? new QuestBundle();
    }

    public static void SaveToFile(string path, QuestBundle bundle)
    {
        var json = JsonSerializer.Serialize(bundle, Options);
        File.WriteAllText(path, json);
    }
}
