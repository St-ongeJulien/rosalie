using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Platform;
using HtmlAgilityPack;
using Hylterium.QuestStudio.Models;

namespace Hylterium.QuestStudio.Services;

/// <summary>
/// Loads a catalog of Hytale items.
/// 
/// The app supports two sources:
/// - Bundled HTML (offline): <c>avares://Hylterium.QuestStudio/Assets/item-ids.html</c>
/// - Online (optional): downloads from HytaleGuide.net (markup may change)
/// 
/// The parser is defensive and tries to handle both the new table markup
/// (img tag in td-img, item id in td-id/code) and the older markup.
/// </summary>
public static class HytaleGuideItemsService
{
    private const string DefaultItemsUrl = "https://hytaleguide.net/item-ids";

    private static readonly Uri BundledItemsUri = new("avares://Hylterium.QuestStudio/Assets/item-ids.html");

    /// <summary>
    /// Loads the bundled HTML shipped with the app (offline-friendly).
    /// </summary>
    public static async Task<List<GameItemEntry>> LoadBundledAsync()
    {
        try
        {
            if (!AssetLoader.Exists(BundledItemsUri))
                return new List<GameItemEntry>();

            await using var stream = AssetLoader.Open(BundledItemsUri);
            using var reader = new System.IO.StreamReader(stream);
            var html = await reader.ReadToEndAsync();
            return ParseHtml(html);
        }
        catch
        {
            return new List<GameItemEntry>();
        }
    }

    /// <summary>
    /// Downloads the items page from the web and parses it.
    /// </summary>
    public static async Task<List<GameItemEntry>> DownloadAsync(string? url = null)
    {
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("HylteriumQuestStudio/1.0 (+offline-cache)");

            var html = await http.GetStringAsync(url ?? DefaultItemsUrl);
            return ParseHtml(html);
        }
        catch
        {
            return new List<GameItemEntry>();
        }
    }

    private static List<GameItemEntry> ParseHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return new List<GameItemEntry>();

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // New markup: <tbody><tr>...<td class="td-img"><img .../></td> ... <td class="td-id"><code>...</code></td>
        var rows = doc.DocumentNode.SelectNodes("//tbody//tr");
        if (rows is null || rows.Count == 0)
            return new List<GameItemEntry>();

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<GameItemEntry>(rows.Count);

        foreach (var row in rows)
        {
            try
            {
                // Name
                var nameNode = row.SelectSingleNode(".//td[contains(@class,'td-name')]//a")
                               ?? row.SelectSingleNode(".//a[contains(@href,'/item/')]");

                var name = WebUtility.HtmlDecode(nameNode?.InnerText ?? string.Empty).Trim();

                // Item id
                var idNode = row.SelectSingleNode(".//td[contains(@class,'td-id')]//code")
                            ?? row.SelectSingleNode(".//code");

                var id = WebUtility.HtmlDecode(idNode?.InnerText ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(id))
                    continue;

                // Image URL (new markup uses <img src="...")
                var img = row.SelectSingleNode(".//td[contains(@class,'td-img')]//img")
                             ?.GetAttributeValue("src", null);

                // Older markup fallback: a link with text "Image"
                if (string.IsNullOrWhiteSpace(img))
                {
                    img = row.SelectSingleNode(".//a[normalize-space()='Image']")
                             ?.GetAttributeValue("href", null);
                }

                img = NormalizeImageUrl(img);

                // De-dupe by id
                if (!seen.Add(id))
                    continue;

                results.Add(new GameItemEntry
                {
                    Id = id,
                    Name = string.IsNullOrWhiteSpace(name) ? id : name,
                    ImageUrl = img
                });
            }
            catch
            {
                // Skip broken rows
            }
        }

        // Stable ordering
        return results
            .OrderBy(x => x.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? NormalizeImageUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        url = url.Trim();

        // Protocol-relative
        if (url.StartsWith("//", StringComparison.Ordinal))
            return "https:" + url;

        // Relative to site
        if (url.StartsWith("/", StringComparison.Ordinal))
            return "https://hytaleguide.net" + url;

        return url;
    }
}
