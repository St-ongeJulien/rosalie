using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Hylterium.QuestStudio.Models;

namespace Hylterium.QuestStudio.Services;

public static class HytaleGuideItemsService
{
    public const string SourceUrl = "https://hytaleguide.net/item-ids";

    public static async Task<List<GameItemEntry>> DownloadAsync(CancellationToken ct = default)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("HylteriumQuestStudio/1.0 (+https://flowlium.com)");
        var html = await http.GetStringAsync(SourceUrl, ct);

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // HytaleGuide lists rows with a link labeled 'Image' (direct CDN png), followed by the item name,
        // and later a <code>Item_Id</code>.
        var imageLinks = doc.DocumentNode.SelectNodes("//a[normalize-space(text())='Image' and contains(@href,'cdn.hytaleguide.net')]");
        if (imageLinks is null || imageLinks.Count == 0)
            return new List<GameItemEntry>();

        var results = new List<GameItemEntry>(imageLinks.Count);

        foreach (var imgLink in imageLinks)
        {
            ct.ThrowIfCancellationRequested();

            var imgUrl = imgLink.GetAttributeValue("href", string.Empty);
            if (string.IsNullOrWhiteSpace(imgUrl))
                continue;

            var nameNode = imgLink.SelectSingleNode("following::a[normalize-space(text())!='' and normalize-space(text())!='Image'][1]");
            var name = HtmlEntity.DeEntitize(nameNode?.InnerText ?? string.Empty).Trim();

            var codeNode = (nameNode ?? imgLink).SelectSingleNode("following::code[1]");
            var itemId = HtmlEntity.DeEntitize(codeNode?.InnerText ?? string.Empty).Trim().Trim('`', ' ', '\n', '\r', '\t');

            if (string.IsNullOrWhiteSpace(itemId) || !Regex.IsMatch(itemId, "^[A-Za-z0-9_]+$"))
            {
                // Fallback: try to find an ID-like token near the row
                var rowText = HtmlEntity.DeEntitize((codeNode?.ParentNode ?? nameNode?.ParentNode ?? imgLink.ParentNode)?.InnerText ?? string.Empty);
                var m = Regex.Match(rowText ?? string.Empty, "\\b[A-Za-z0-9_]{8,}\\b");
                if (m.Success) itemId = m.Value;
            }

            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            if (string.IsNullOrWhiteSpace(name))
                name = ItemsCatalogService.Humanize(itemId);

            results.Add(new GameItemEntry
            {
                Id = itemId,
                Name = name,
                ImageUrl = imgUrl
            });
        }

        // Deduplicate + stable sort
        return results
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
