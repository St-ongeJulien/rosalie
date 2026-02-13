using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Hylterium.QuestStudio.Models;
using Hylterium.QuestStudio.Services;

namespace Hylterium.QuestStudio.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    // --- InfoBar ---
    [ObservableProperty] private string _status = "Prêt. Ouvre un JSON de NPC Dialog / Quests Maker.";
    [ObservableProperty] private bool _isInfoBarOpen = true;
    [ObservableProperty] private string _infoTitle = "Hylterium Quest Studio";
    [ObservableProperty] private InfoBarSeverity _infoSeverity = InfoBarSeverity.Informational;

    [ObservableProperty] private string? _currentFilePath;

    // --- Quests ---
    public ObservableCollection<NpcDef> Npcs { get; } = new();
    public ObservableCollection<NpcDef> VisibleNpcs { get; } = new();

    [ObservableProperty] private string _npcSearch = "";

    [ObservableProperty] private NpcDef? _selectedNpc;

    public ObservableCollection<DialogPage> VisiblePages { get; } = new();
    [ObservableProperty] private string _pageSearch = "";

    [ObservableProperty] private DialogPage? _selectedPage;

    public ObservableCollection<string> QuestIds { get; } = new();

    public QuestBundle Bundle { get; private set; } = new();

    // --- Assets ---
    public ObservableCollection<AssetPack> AssetPacks { get; } = new();
    public ObservableCollection<AssetPack> VisibleAssetPacks { get; } = new();

    [ObservableProperty] private string _packSearch = "";
    [ObservableProperty] private AssetPack? _selectedAssetPack;

    public ObservableCollection<AssetEntry> VisibleAssets { get; } = new();
    [ObservableProperty] private string _assetSearch = "";
    [ObservableProperty] private AssetEntry? _selectedAsset;

    [ObservableProperty] private Bitmap? _selectedAssetPreview;

    public string DefaultHytaleModsPath => AssetPackService.GetDefaultHytaleModsPath();

    public bool HasSelectedAsset => SelectedAsset is not null;
    public bool HasSelectedNpcAndAsset => SelectedNpc is not null && SelectedAsset is not null;

    partial void OnSelectedAssetChanged(AssetEntry? value)
    {
        OnPropertyChanged(nameof(HasSelectedAsset));
        OnPropertyChanged(nameof(HasSelectedNpcAndAsset));
        _ = LoadSelectedAssetPreviewAsync(value);
    }

    partial void OnSelectedNpcChanged(NpcDef? value)
    {
        ApplyPageFilter();
        SelectedPage = VisiblePages.FirstOrDefault();
        OnPropertyChanged(nameof(HasSelectedNpcAndAsset));
    }

    partial void OnNpcSearchChanged(string value) => ApplyNpcFilter();
    partial void OnPageSearchChanged(string value) => ApplyPageFilter();
    partial void OnPackSearchChanged(string value) => ApplyPackFilter();
    partial void OnAssetSearchChanged(string value) => ApplyAssetFilter();

    partial void OnSelectedAssetPackChanged(AssetPack? value)
    {
        ApplyAssetFilter();
    }

    public void LoadBundle(QuestBundle bundle, string? filePath)
    {
        Bundle = bundle;
        CurrentFilePath = filePath;

        Npcs.Clear();
        foreach (var npc in bundle.Npcs) Npcs.Add(npc);

        ApplyNpcFilter();
        SelectedNpc = VisibleNpcs.FirstOrDefault();

        RefreshQuestIds();

        SetInfo(filePath is null ? "Bundle chargé." : $"Chargé: {filePath}");
    }

    public QuestBundle BuildBundleFromUi()
    {
        Bundle.Npcs = Npcs.ToList();
        return Bundle;
    }

    public void RefreshQuestIds()
    {
        QuestIds.Clear();
        foreach (var id in QuestIdScanner.Scan(Bundle))
            QuestIds.Add(id);
    }

    private void ApplyNpcFilter()
    {
        var term = (NpcSearch ?? "").Trim();
        var list = string.IsNullOrWhiteSpace(term)
            ? Npcs.ToList()
            : Npcs.Where(n =>
                    (n.Name ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (n.DisplayTitle ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (n.NpcId ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (n.EntityId ?? "").Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();

        VisibleNpcs.Clear();
        foreach (var n in list) VisibleNpcs.Add(n);

        if (SelectedNpc is null || !VisibleNpcs.Contains(SelectedNpc))
            SelectedNpc = VisibleNpcs.FirstOrDefault();
    }

    private void ApplyPageFilter()
    {
        VisiblePages.Clear();
        if (SelectedNpc?.Pages is null) return;

        var term = (PageSearch ?? "").Trim();
        var list = string.IsNullOrWhiteSpace(term)
            ? SelectedNpc.Pages.ToList()
            : SelectedNpc.Pages.Where(p =>
                    (p.PageId ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (p.Title ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (p.Content ?? "").Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();

        foreach (var p in list) VisiblePages.Add(p);

        if (SelectedPage is null || !VisiblePages.Contains(SelectedPage))
            SelectedPage = VisiblePages.FirstOrDefault();
    }

    // --- Asset packs ---

    public void AddAssetPack(string path)
    {
        var pack = AssetPackService.Load(path);
        AssetPacks.Add(pack);
        ApplyPackFilter();
        SelectedAssetPack = pack;
    }

    public int ScanDefaultHytaleMods()
    {
        var modsPath = DefaultHytaleModsPath;
        if (!Directory.Exists(modsPath))
            throw new DirectoryNotFoundException($"Dossier introuvable: {modsPath}");

        var discovered = AssetPackService.DiscoverPacksInModsFolder(modsPath).ToList();
        var added = 0;

        foreach (var p in discovered)
        {
            // Avoid duplicates by path
            if (AssetPacks.Any(x => string.Equals(x.SourcePath, p, StringComparison.OrdinalIgnoreCase)))
                continue;

            try
            {
                var pack = AssetPackService.Load(p);
                AssetPacks.Add(pack);
                added++;
            }
            catch
            {
                // Ignore broken packs; mods folder can contain many things
            }
        }

        ApplyPackFilter();
        return added;
    }

    private void ApplyPackFilter()
    {
        var term = (PackSearch ?? "").Trim();

        var list = string.IsNullOrWhiteSpace(term)
            ? AssetPacks.ToList()
            : AssetPacks.Where(p =>
                    (p.Name ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (p.SourcePath ?? "").Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();

        VisibleAssetPacks.Clear();
        foreach (var p in list) VisibleAssetPacks.Add(p);

        if (SelectedAssetPack is null || !VisibleAssetPacks.Contains(SelectedAssetPack))
            SelectedAssetPack = VisibleAssetPacks.FirstOrDefault();
    }

    private void ApplyAssetFilter()
    {
        VisibleAssets.Clear();
        SelectedAsset = null;
        SelectedAssetPreview = null;

        if (SelectedAssetPack is null) return;

        var term = (AssetSearch ?? "").Trim();
        var list = string.IsNullOrWhiteSpace(term)
            ? SelectedAssetPack.Entries
            : SelectedAssetPack.Entries
                .Where(e => (e.RelativePath ?? "").Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();

        foreach (var a in list.Take(8000))
            VisibleAssets.Add(a);

        if (list.Count > 8000)
            SetInfo("Liste tronquée à 8000 assets pour la performance.", title: "Assets");
    }

    private async Task LoadSelectedAssetPreviewAsync(AssetEntry? entry)
    {
        if (entry is null)
        {
            await Dispatcher.UIThread.InvokeAsync(() => SelectedAssetPreview = null);
            return;
        }

        try
        {
            await using var stream = AssetPackService.Open(entry);
            if (stream is null)
            {
                await Dispatcher.UIThread.InvokeAsync(() => SelectedAssetPreview = null);
                return;
            }

            // Decode with a reasonable max width for preview
            var bmp = await Task.Run(() => Bitmap.DecodeToWidth(stream, 600));
            await Dispatcher.UIThread.InvokeAsync(() => SelectedAssetPreview = bmp);
        }
        catch
        {
            await Dispatcher.UIThread.InvokeAsync(() => SelectedAssetPreview = null);
        }
    }

    // --- Commands ---

    [RelayCommand]
    private void NewNpc()
    {
        var npc = new NpcDef
        {
            NpcId = Guid.NewGuid().ToString(),
            Name = "Nouveau NPC",
            DisplayTitle = "Titre",
            FirstPageId = "page_1",
            Pages = new List<DialogPage>
            {
                new DialogPage{ PageId = "page_1", Title = "Page 1", Content = "Contenu..." }
            }
        };

        Npcs.Add(npc);
        ApplyNpcFilter();
        SelectedNpc = npc;
        SelectedPage = npc.Pages.FirstOrDefault();

        Bundle.Npcs = Npcs.ToList();
        RefreshQuestIds();
        SetInfo("NPC ajouté (brouillon).", title: "Quêtes");
    }

    [RelayCommand]
    private void NewPage()
    {
        if (SelectedNpc is null) return;

        var nextIndex = (SelectedNpc.Pages?.Count ?? 0) + 1;
        var pageId = $"page_{nextIndex}";
        var page = new DialogPage { PageId = pageId, Title = $"Page {nextIndex}", Content = "Contenu..." };

        SelectedNpc.Pages ??= new List<DialogPage>();
        SelectedNpc.Pages.Add(page);

        // set firstPageId if empty
        if (string.IsNullOrWhiteSpace(SelectedNpc.FirstPageId))
            SelectedNpc.FirstPageId = pageId;

        ApplyPageFilter();
        SelectedPage = page;

        Bundle.Npcs = Npcs.ToList();
        RefreshQuestIds();
        SetInfo($"Page ajoutée: {pageId}", title: "Quêtes");
    }

    [RelayCommand]
    private void DeleteSelectedPage()
    {
        if (SelectedNpc is null || SelectedPage is null) return;
        var idx = SelectedNpc.Pages.IndexOf(SelectedPage);
        if (idx < 0) return;

        SelectedNpc.Pages.RemoveAt(idx);
        ApplyPageFilter();
        SelectedPage = VisiblePages.FirstOrDefault();

        Bundle.Npcs = Npcs.ToList();
        RefreshQuestIds();
        SetInfo("Page supprimée.", title: "Quêtes");
    }

    // --- Helpers for InfoBar ---

    public void SetInfo(string message, string? title = null)
    {
        InfoTitle = title ?? "Info";
        InfoSeverity = InfoBarSeverity.Informational;
        Status = message;
        IsInfoBarOpen = true;
    }

    public void SetError(string message, string? title = null)
    {
        InfoTitle = title ?? "Erreur";
        InfoSeverity = InfoBarSeverity.Error;
        Status = message;
        IsInfoBarOpen = true;
    }
}
