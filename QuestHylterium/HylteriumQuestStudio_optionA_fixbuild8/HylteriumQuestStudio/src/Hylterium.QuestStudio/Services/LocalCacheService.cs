using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Hylterium.QuestStudio.Models;

namespace Hylterium.QuestStudio.Services;

public static class LocalCacheService
{
    public static string AppDataRoot
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HylteriumQuestStudio");

    public static string ItemsIndexPath => Path.Combine(AppDataRoot, "items_index.json");
    public static string ItemImagesDir => Path.Combine(AppDataRoot, "item_images");

    public static void EnsureFolders()
    {
        Directory.CreateDirectory(AppDataRoot);
        Directory.CreateDirectory(ItemImagesDir);
    }

    public static bool TryLoadItemsIndex(out List<GameItemEntry> items)
    {
        try
        {
            EnsureFolders();
            if (!File.Exists(ItemsIndexPath))
            {
                items = new List<GameItemEntry>();
                return false;
            }

            var json = File.ReadAllText(ItemsIndexPath);
            var dto = JsonSerializer.Deserialize<List<ItemIndexDto>>(json) ?? new List<ItemIndexDto>();

            items = new List<GameItemEntry>(dto.Count);
            foreach (var d in dto)
            {
                if (string.IsNullOrWhiteSpace(d.ItemId))
                    continue;

                items.Add(new GameItemEntry
                {
                    Id = d.ItemId!,
                    Name = string.IsNullOrWhiteSpace(d.Name) ? ItemsCatalogService.Humanize(d.ItemId!) : d.Name!,
                    ImageUrl = d.ImageUrl
                });
            }

            return items.Count > 0;
        }
        catch
        {
            items = new List<GameItemEntry>();
            return false;
        }
    }

    public static void SaveItemsIndex(IEnumerable<GameItemEntry> items)
    {
        EnsureFolders();

        var dto = new List<ItemIndexDto>();
        foreach (var it in items)
        {
            if (string.IsNullOrWhiteSpace(it.Id))
                continue;

            dto.Add(new ItemIndexDto
            {
                ItemId = it.Id,
                Name = it.Name,
                ImageUrl = it.ImageUrl
            });
        }

        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ItemsIndexPath, json);
    }

    private sealed class ItemIndexDto
    {
        public string? ItemId { get; set; }
        public string? Name { get; set; }
        public string? ImageUrl { get; set; }
    }
}
