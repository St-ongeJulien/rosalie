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
        var text = File.ReadAllText(path);
        var trimmed = text.Trim();

        // If it's JSON, try to read full entries (itemId/name/imageUrl) OR simple ids.
        if (trimmed.StartsWith("[") || trimmed.StartsWith("{"))
        {
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                var entries = TryParseEntries(doc.RootElement);
                if (entries.Count > 0)
                    return entries;
            }
            catch
            {
                // fall back to id-only parsing
            }
        }

        // Fallback: ids only (JSON strings / { items: [...] } / newline-separated)
        var ids = LoadItemIds(path);
        return ids.Select(id => new GameItemEntry { Id = id, Name = Humanize(id) }).ToList();
    }

    /// <summary>
    /// Parses a JSON payload into items.
    /// Supports the same formats as <see cref="LoadFromJsonFile"/>.
    /// Returns an empty list if parsing fails.
    /// </summary>
    public static List<GameItemEntry> LoadFromJsonString(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<GameItemEntry>();

        var trimmed = json.Trim();
        if (!(trimmed.StartsWith("[") || trimmed.StartsWith("{")))
            return new List<GameItemEntry>();

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            return TryParseEntries(doc.RootElement);
        }
        catch
        {
            return new List<GameItemEntry>();
        }
    }

    private static List<GameItemEntry> TryParseEntries(JsonElement root)
    {
        // Supports:
        // - [ { itemId: "...", name: "...", imageUrl: "..." }, ...]
        // - [ "Item_Id", ...]
        // - { items: [...] }
        // - { items: [ { ... }, ...] }

        static string? GetString(JsonElement obj, params string[] names)
        {
            foreach (var n in names)
            {
                if (obj.TryGetProperty(n, out var p) && p.ValueKind == JsonValueKind.String)
                    return p.GetString();
            }
            return null;
        }

        static GameItemEntry? ToEntry(JsonElement e)
        {
            if (e.ValueKind == JsonValueKind.String)
            {
                string idstring = e.GetString();
                if (string.IsNullOrWhiteSpace(idstring)) return null;
                idstring = idstring.Trim();
                return new GameItemEntry { Id = idstring, Name = Humanize(idstring) };
            }

            if (e.ValueKind != JsonValueKind.Object)
                return null;

            var id = GetString(e, "itemId", "id", "Id", "ItemId");
            if (string.IsNullOrWhiteSpace(id)) return null;
            id = id.Trim();

            var name = GetString(e, "name", "Name")?.Trim();
            var imageUrl = GetString(e, "imageUrl", "ImageUrl")?.Trim();

            return new GameItemEntry
            {
                Id = id,
                Name = string.IsNullOrWhiteSpace(name) ? Humanize(id) : name,
                ImageUrl = imageUrl
            };
        }

        IEnumerable<JsonElement> EnumerateElements(JsonElement el)
        {
            if (el.ValueKind == JsonValueKind.Array)
                return el.EnumerateArray();

            if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
                return items.EnumerateArray();

            return Array.Empty<JsonElement>();
        }

        var list = new List<GameItemEntry>();
        foreach (var e in EnumerateElements(root))
        {
            var entry = ToEntry(e);
            if (entry is null || string.IsNullOrWhiteSpace(entry.Id))
                continue;
            list.Add(entry);
        }

        // Deduplicate by id (keep first with image)
        return list
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => !string.IsNullOrWhiteSpace(x.ImageUrl)).First())
            .ToList();
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
