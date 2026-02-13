using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using Hylterium.QuestStudio.Models;

namespace Hylterium.QuestStudio.Services;

public static class AssetPackService
{
    private static readonly HashSet<string> ImageExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp", ".bmp"
    };

    public static string GetDefaultHytaleModsPath()
    {
        // Based on common Windows path:
        // C:\Users\<you>\AppData\Roaming\Hytale\UserData\Mods
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "Hytale", "UserData", "Mods");
    }

    public static IEnumerable<string> DiscoverPacksInModsFolder(string modsFolder)
    {
        if (!Directory.Exists(modsFolder)) yield break;

        foreach (var dir in Directory.EnumerateDirectories(modsFolder))
        {
            if (File.Exists(Path.Combine(dir, "manifest.json")))
                yield return dir;
        }

        foreach (var file in Directory.EnumerateFiles(modsFolder, "*.zip"))
        {
            // Many mods are zip packages
            yield return file;
        }
    }

    public static AssetPack Load(string path)
    {
        if (Directory.Exists(path))
            return LoadFromFolder(path);

        if (File.Exists(path) && Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            return LoadFromZip(path);

        throw new FileNotFoundException("Pack introuvable ou format non supporté", path);
    }

    public static Stream? Open(AssetEntry entry)
    {
        if (entry.PackIsZip)
        {
            var zip = ZipFile.OpenRead(entry.PackSourcePath);
            var ze = zip.GetEntry(entry.RelativePath.Replace('\\', '/'));
            if (ze is null)
            {
                zip.Dispose();
                return null;
            }

            // We must keep the zip alive while the stream is used.
            // So we return a wrapper stream that disposes zip on dispose.
            return new ZipEntryStream(zip, ze.Open());
        }

        var fullPath = Path.Combine(entry.PackSourcePath, entry.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath)) return null;
        return File.OpenRead(fullPath);
    }

    private static AssetPack LoadFromFolder(string folder)
    {
        var pack = new AssetPack
        {
            SourcePath = folder,
            IsZip = false,
            Name = ReadManifestNameFromFolder(folder) ?? Path.GetFileName(folder)
        };

        var files = Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories)
            .Where(f => ImageExt.Contains(Path.GetExtension(f)))
            .Select(f => new FileInfo(f))
            .OrderBy(f => f.FullName, StringComparer.OrdinalIgnoreCase)
            .Take(20000)
            .ToList();

        foreach (var fi in files)
        {
            var rel = Path.GetRelativePath(folder, fi.FullName).Replace('\\', '/');
            pack.Entries.Add(new AssetEntry
            {
                PackIsZip = false,
                PackSourcePath = folder,
                RelativePath = rel,
                Size = fi.Length
            });
        }

        return pack;
    }

    private static AssetPack LoadFromZip(string zipPath)
    {
        var pack = new AssetPack
        {
            SourcePath = zipPath,
            IsZip = true,
            Name = ReadManifestNameFromZip(zipPath) ?? Path.GetFileNameWithoutExtension(zipPath)
        };

        using var zip = ZipFile.OpenRead(zipPath);
        var entries = zip.Entries
            .Where(e => !string.IsNullOrWhiteSpace(e.Name))
            .Where(e => ImageExt.Contains(Path.GetExtension(e.Name)))
            .OrderBy(e => e.FullName, StringComparer.OrdinalIgnoreCase)
            .Take(20000)
            .ToList();

        foreach (var e in entries)
        {
            pack.Entries.Add(new AssetEntry
            {
                PackIsZip = true,
                PackSourcePath = zipPath,
                RelativePath = e.FullName.Replace('\\', '/'),
                Size = e.Length
            });
        }

        return pack;
    }

    private static string? ReadManifestNameFromFolder(string folder)
    {
        var path = Path.Combine(folder, "manifest.json");
        if (!File.Exists(path)) return null;

        try
        {
            var json = File.ReadAllText(path);
            return ReadManifestName(json);
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadManifestNameFromZip(string zipPath)
    {
        try
        {
            using var zip = ZipFile.OpenRead(zipPath);
            var man = zip.Entries.FirstOrDefault(e =>
                string.Equals(e.FullName, "manifest.json", StringComparison.OrdinalIgnoreCase));

            if (man is null) return null;
            using var s = man.Open();
            using var r = new StreamReader(s);
            var json = r.ReadToEnd();
            return ReadManifestName(json);
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadManifestName(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Common patterns: {"name": "..."} or {"mod": {"name": "..."}}
            if (root.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
                return name.GetString();

            if (root.TryGetProperty("mod", out var mod) && mod.ValueKind == JsonValueKind.Object)
            {
                if (mod.TryGetProperty("name", out var modName) && modName.ValueKind == JsonValueKind.String)
                    return modName.GetString();

                if (mod.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
                    return id.GetString();
            }

            if (root.TryGetProperty("id", out var rid) && rid.ValueKind == JsonValueKind.String)
                return rid.GetString();
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private sealed class ZipEntryStream : Stream
    {
        private readonly ZipArchive _zip;
        private readonly Stream _inner;

        public ZipEntryStream(ZipArchive zip, Stream inner)
        {
            _zip = zip;
            _inner = inner;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }

        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
                _zip.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
