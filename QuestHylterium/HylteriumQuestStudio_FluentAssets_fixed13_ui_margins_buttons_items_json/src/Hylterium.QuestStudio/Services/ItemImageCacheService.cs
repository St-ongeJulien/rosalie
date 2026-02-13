using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Hylterium.QuestStudio.Models;

namespace Hylterium.QuestStudio.Services;

public static class ItemImageCacheService
{
    public static async Task<string?> GetOrDownloadImagePathAsync(GameItemEntry item, CancellationToken ct = default)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.ImageUrl) || string.IsNullOrWhiteSpace(item.Id))
            return null;

        LocalCacheService.EnsureFolders();

        var safeId = MakeSafeFileName(item.Id);
        var ext = ".png";

        try
        {
            var uri = new Uri(item.ImageUrl);
            var uriExt = Path.GetExtension(uri.AbsolutePath);
            if (!string.IsNullOrWhiteSpace(uriExt) && uriExt.Length <= 5)
                ext = uriExt;
        }
        catch
        {
            // ignored
        }

        var path = Path.Combine(LocalCacheService.ItemImagesDir, safeId + ext);

        if (File.Exists(path))
        {
            item.LocalImagePath = path;
            return path;
        }

        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("HylteriumQuestStudio/1.0");
        var bytes = await http.GetByteArrayAsync(item.ImageUrl, ct);

        await File.WriteAllBytesAsync(path, bytes, ct);
        item.LocalImagePath = path;
        return path;
    }

    public static async Task<Bitmap?> LoadBitmapAsync(string path, int decodeWidth, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        // Decode off UI thread (Bitmap decoding can be costly)
        return await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            using var fs = File.OpenRead(path);
            return Bitmap.DecodeToWidth(fs, decodeWidth);
        }, ct);
    }

    private static string MakeSafeFileName(string input)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            input = input.Replace(c, '_');
        return input;
    }
}
