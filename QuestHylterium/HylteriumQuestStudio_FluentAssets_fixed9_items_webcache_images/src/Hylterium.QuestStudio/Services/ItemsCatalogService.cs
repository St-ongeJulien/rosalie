using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Hylterium.QuestStudio.Models;

namespace Hylterium.QuestStudio.Services;

public static class ItemsCatalogService
{
    /// <summary>
    /// Loads items ids from a user-provided file.
    /// Supported formats:
    /// - JSON array of strings: ["Plant_Crop_Lettuce_Item", ...]
    /// - JSON object with "items": ["..."]
    /// - newline-separated text file
    /// </summary>
    public static List<string> LoadItemIds(string path)
    {
        var text = File.ReadAllText(path);
        var trimmed = text.Trim();

        // JSON?
        if (trimmed.StartsWith("[") || trimmed.StartsWith("{"))
        {
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    return doc.RootElement.EnumerateArray()
                        .Where(e => e.ValueKind == JsonValueKind.String)
                        .Select(e => e.GetString() ?? "")
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                    doc.RootElement.TryGetProperty("items", out var items) &&
                    items.ValueKind == JsonValueKind.Array)
                    return items.EnumerateArray()
                        .Where(e => e.ValueKind == JsonValueKind.String)
                        .Select(e => e.GetString() ?? "")
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
            }
            catch
            {
                // fallback to text parsing below
            }
        }

        // text (newline-separated)
        return trimmed
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Best-effort mapping: pick the first image entry that contains the id in the path.
    /// </summary>
    public static AssetEntry? TryFindIconForId(IEnumerable<AssetPack> packs, string id)
    {
        var needle = id.Replace(" ", "").ToLowerInvariant();

        foreach (var p in packs)
        {
            var hit = p.Entries.FirstOrDefault(e =>
            {
                var rp = (e.RelativePath ?? "").ToLowerInvariant();
                if (!(rp.EndsWith(".png") || rp.EndsWith(".jpg") || rp.EndsWith(".jpeg") || rp.EndsWith(".webp")))
                    return false;
                return rp.Contains(needle);
            });

            if (hit is not null)
                return hit;
        }

        return null;
    }


    public static List<GameItemEntry> LoadFromJsonFile(string path)
    {
        var ids = LoadItemIds(path);
        return ids.Select(id => new GameItemEntry
        {
            Id = id,
            Name = Humanize(id)
        }).ToList();
    }

    public static void SaveToJsonFile(string path, IEnumerable<GameItemEntry> items)
    {
        var payload = items
            .Where(i => !string.IsNullOrWhiteSpace(i.Id))
            .Select(i => new
            {
                itemId = i.Id,
                name = i.Name,
                imageUrl = i.ImageUrl
            })
            .ToList();

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    public static string Humanize(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return id;
        // strip common suffixes and underscores
        var s = id;
        if (s.EndsWith("_Item", StringComparison.OrdinalIgnoreCase))
            s = s[..^5];
        s = s.Replace('_', ' ').Trim();
        return s;
    }
}
