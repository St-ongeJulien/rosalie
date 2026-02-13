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


    // --- Page editor (editable requirements / rewards) ---
    public ObservableCollection<ItemRequirement> NextItemRequirements { get; } = new();
    [ObservableProperty] private ItemRequirement? _selectedNextItemRequirement;

    public ObservableCollection<CustomRequirement> NextCustomRequirements { get; } = new();
    [ObservableProperty] private CustomRequirement? _selectedNextCustomRequirement;

    public ObservableCollection<ItemRequirement> Custom1ItemRequirements { get; } = new();
    [ObservableProperty] private ItemRequirement? _selectedCustom1ItemRequirement;

    public ObservableCollection<CustomRequirement> Custom1CustomRequirements { get; } = new();
    [ObservableProperty] private CustomRequirement? _selectedCustom1CustomRequirement;

    public ObservableCollection<ItemRequirement> Custom2ItemRequirements { get; } = new();
    [ObservableProperty] private ItemRequirement? _selectedCustom2ItemRequirement;

    public ObservableCollection<CustomRequirement> Custom2CustomRequirements { get; } = new();
    [ObservableProperty] private CustomRequirement? _selectedCustom2CustomRequirement;

    public ObservableCollection<CustomReward> Custom1CustomRewards { get; } = new();
    [ObservableProperty] private CustomReward? _selectedCustom1CustomReward;

    public ObservableCollection<ConsoleCommandRow> Custom1ConsoleCommands { get; } = new();
    [ObservableProperty] private ConsoleCommandRow? _selectedCustom1ConsoleCommand;

    public ObservableCollection<CustomReward> Custom2CustomRewards { get; } = new();
    [ObservableProperty] private CustomReward? _selectedCustom2CustomReward;

    public ObservableCollection<ConsoleCommandRow> Custom2ConsoleCommands { get; } = new();
    [ObservableProperty] private ConsoleCommandRow? _selectedCustom2ConsoleCommand;


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

    partial void OnSelectedPageChanging(DialogPage? value) => SyncEditorListsToPage();

    partial void OnSelectedPageChanged(DialogPage? value)
    {
        LoadEditorListsFromPage();
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
        SyncEditorListsToPage();
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

    
    // --- Requirement / Reward editors ---
    [RelayCommand]
    private void AddNextItemRequirement()
    {
        if (SelectedPage is null) return;
        NextItemRequirements.Add(new ItemRequirement { ItemId = "", Quantity = 1, Consume = true });
        SetInfo("Prérequis (Next): item ajouté.", title: "Éditeur");
    }

    [RelayCommand]
    private void RemoveNextItemRequirement()
    {
        if (SelectedNextItemRequirement is null) return;
        NextItemRequirements.Remove(SelectedNextItemRequirement);
        SelectedNextItemRequirement = null;
        SetInfo("Prérequis (Next): item supprimé.", title: "Éditeur");
    }

    [RelayCommand]
    private void AddNextCustomRequirement()
    {
        if (SelectedPage is null) return;
        NextCustomRequirements.Add(new CustomRequirement { RequirementId = "", Amount = 1 });
        SetInfo("Prérequis (Next): custom ajouté.", title: "Éditeur");
    }

    [RelayCommand]
    private void RemoveNextCustomRequirement()
    {
        if (SelectedNextCustomRequirement is null) return;
        NextCustomRequirements.Remove(SelectedNextCustomRequirement);
        SelectedNextCustomRequirement = null;
        SetInfo("Prérequis (Next): custom supprimé.", title: "Éditeur");
    }

    [RelayCommand]
    private void AddCustom1ItemRequirement()
    {
        if (SelectedPage is null) return;
        Custom1ItemRequirements.Add(new ItemRequirement { ItemId = "", Quantity = 1, Consume = true });
        SetInfo("Prérequis (Custom1): item ajouté.", title: "Éditeur");
    }

    [RelayCommand]
    private void RemoveCustom1ItemRequirement()
    {
        if (SelectedCustom1ItemRequirement is null) return;
        Custom1ItemRequirements.Remove(SelectedCustom1ItemRequirement);
        SelectedCustom1ItemRequirement = null;
        SetInfo("Prérequis (Custom1): item supprimé.", title: "Éditeur");
    }

    [RelayCommand]
    private void AddCustom1CustomRequirement()
    {
        if (SelectedPage is null) return;
        Custom1CustomRequirements.Add(new CustomRequirement { RequirementId = "", Amount = 1 });
        SetInfo("Prérequis (Custom1): custom ajouté.", title: "Éditeur");
    }

    [RelayCommand]
    private void RemoveCustom1CustomRequirement()
    {
        if (SelectedCustom1CustomRequirement is null) return;
        Custom1CustomRequirements.Remove(SelectedCustom1CustomRequirement);
        SelectedCustom1CustomRequirement = null;
        SetInfo("Prérequis (Custom1): custom supprimé.", title: "Éditeur");
    }

    [RelayCommand]
    private void AddCustom2ItemRequirement()
    {
        if (SelectedPage is null) return;
        Custom2ItemRequirements.Add(new ItemRequirement { ItemId = "", Quantity = 1, Consume = true });
        SetInfo("Prérequis (Custom2): item ajouté.", title: "Éditeur");
    }

    [RelayCommand]
    private void RemoveCustom2ItemRequirement()
    {
        if (SelectedCustom2ItemRequirement is null) return;
        Custom2ItemRequirements.Remove(SelectedCustom2ItemRequirement);
        SelectedCustom2ItemRequirement = null;
        SetInfo("Prérequis (Custom2): item supprimé.", title: "Éditeur");
    }

    [RelayCommand]
    private void AddCustom2CustomRequirement()
    {
        if (SelectedPage is null) return;
        Custom2CustomRequirements.Add(new CustomRequirement { RequirementId = "", Amount = 1 });
        SetInfo("Prérequis (Custom2): custom ajouté.", title: "Éditeur");
    }

    [RelayCommand]
    private void RemoveCustom2CustomRequirement()
    {
        if (SelectedCustom2CustomRequirement is null) return;
        Custom2CustomRequirements.Remove(SelectedCustom2CustomRequirement);
        SelectedCustom2CustomRequirement = null;
        SetInfo("Prérequis (Custom2): custom supprimé.", title: "Éditeur");
    }

    [RelayCommand]
    private void AddCustom1CustomReward()
    {
        if (SelectedPage is null) return;
        Custom1CustomRewards.Add(new CustomReward { RewardId = "", Amount = 1 });
        SetInfo("Récompense (Custom1): reward ajouté.", title: "Éditeur");
    }

    [RelayCommand]
    private void RemoveCustom1CustomReward()
    {
        if (SelectedCustom1CustomReward is null) return;
        Custom1CustomRewards.Remove(SelectedCustom1CustomReward);
        SelectedCustom1CustomReward = null;
        SetInfo("Récompense (Custom1): reward supprimé.", title: "Éditeur");
    }

    [RelayCommand]
    private void AddCustom1ConsoleCommand()
    {
        if (SelectedPage is null) return;
        Custom1ConsoleCommands.Add(new ConsoleCommandRow { Text = "" });
        SetInfo("Récompense (Custom1): commande ajoutée.", title: "Éditeur");
    }

    [RelayCommand]
    private void RemoveCustom1ConsoleCommand()
    {
        if (SelectedCustom1ConsoleCommand is null) return;
        Custom1ConsoleCommands.Remove(SelectedCustom1ConsoleCommand);
        SelectedCustom1ConsoleCommand = null;
        SetInfo("Récompense (Custom1): commande supprimée.", title: "Éditeur");
    }

    [RelayCommand]
    private void AddCustom2CustomReward()
    {
        if (SelectedPage is null) return;
        Custom2CustomRewards.Add(new CustomReward { RewardId = "", Amount = 1 });
        SetInfo("Récompense (Custom2): reward ajouté.", title: "Éditeur");
    }

    [RelayCommand]
    private void RemoveCustom2CustomReward()
    {
        if (SelectedCustom2CustomReward is null) return;
        Custom2CustomRewards.Remove(SelectedCustom2CustomReward);
        SelectedCustom2CustomReward = null;
        SetInfo("Récompense (Custom2): reward supprimé.", title: "Éditeur");
    }

    [RelayCommand]
    private void AddCustom2ConsoleCommand()
    {
        if (SelectedPage is null) return;
        Custom2ConsoleCommands.Add(new ConsoleCommandRow { Text = "" });
        SetInfo("Récompense (Custom2): commande ajoutée.", title: "Éditeur");
    }

    [RelayCommand]
    private void RemoveCustom2ConsoleCommand()
    {
        if (SelectedCustom2ConsoleCommand is null) return;
        Custom2ConsoleCommands.Remove(SelectedCustom2ConsoleCommand);
        SelectedCustom2ConsoleCommand = null;
        SetInfo("Récompense (Custom2): commande supprimée.", title: "Éditeur");
    }


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

public sealed partial class ConsoleCommandRow : ObservableObject
{
    [ObservableProperty] private string _text = string.Empty;
}
