using System.Text.RegularExpressions;
using Hylterium.QuestStudio.Models;

namespace Hylterium.QuestStudio.Services;

public static class QuestIdScanner
{
    // Matches quest:*:<questId> or quest:*:<questId>:...
    private static readonly Regex Rx = new(@"\bquest:[a-z_]+:([a-zA-Z0-9_\-]+)", RegexOptions.Compiled);

    public static IReadOnlyList<string> Scan(QuestBundle bundle)
    {
        var set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var npc in bundle.Npcs)
        {
            ScanPageList(npc.Pages, set);
            if (npc.RequirementPage is not null) ScanPage(npc.RequirementPage, set);
            if (npc.FinishedPage is not null) ScanPage(npc.FinishedPage, set);
        }

        return set.ToList();
    }

    private static void ScanPageList(IEnumerable<DialogPage> pages, SortedSet<string> set)
    {
        foreach (var p in pages) ScanPage(p, set);
    }

    private static void ScanPage(DialogPage page, SortedSet<string> set)
    {
        void scanReq(ButtonRequirement? r)
        {
            if (r?.CustomRequirements is null) return;
            foreach (var cr in r.CustomRequirements)
                AddFromText(cr.RequirementId, set);
        }

        void scanRew(ButtonReward? r)
        {
            if (r?.CustomRewards is not null)
                foreach (var cw in r.CustomRewards)
                    AddFromText(cw.RewardId, set);

            if (r?.ConsoleCommands is not null)
                foreach (var cmd in r.ConsoleCommands)
                    AddFromText(cmd, set);
        }

        scanReq(page.NextButtonRequirement);
        scanReq(page.CustomButton1ButtonRequirement);
        scanReq(page.CustomButton2ButtonRequirement);

        scanRew(page.CustomButton1ButtonReward);
        scanRew(page.CustomButton2ButtonReward);
        AddFromText(page.CloseButtonCommand, set);
        AddFromText(page.Content, set);
        AddFromText(page.Title, set);
    }

    private static void AddFromText(string? text, SortedSet<string> set)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        foreach (Match m in Rx.Matches(text))
        {
            if (m.Groups.Count > 1)
                set.Add(m.Groups[1].Value);
        }
    }
}
