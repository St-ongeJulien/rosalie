using System.Collections.Generic;
using System.IO;

namespace Hylterium.QuestStudio.Models;

public sealed class AssetPack
{
    public string Name { get; set; } = "(pack)";
    public string SourcePath { get; set; } = "";
    public bool IsZip { get; set; }

    public List<AssetEntry> Entries { get; set; } = new();
}

public sealed class AssetEntry
{
    public string PackSourcePath { get; set; } = "";
    public bool PackIsZip { get; set; }

    public string RelativePath { get; set; } = "";
    public long Size { get; set; }

    public string DisplayName => Path.GetFileName(RelativePath);
}
