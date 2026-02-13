using Hylterium.QuestStudio.Models;

namespace Hylterium.QuestStudio.Models;

public sealed class GameItemEntry
{
    public string Id { get; set; } = "";
    public string? DisplayName { get; set; }
    public AssetEntry? Icon { get; set; }
    public string? IconPackPath { get; set; }
}
