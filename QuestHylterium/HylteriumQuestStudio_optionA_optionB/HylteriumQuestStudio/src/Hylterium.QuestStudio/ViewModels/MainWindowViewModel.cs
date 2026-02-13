using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
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

// --- Quest chain (detected from quest:* IDs) ---
[ObservableProperty] private string? _selectedPageQuestId;
[ObservableProperty] private string? _selectedPagePrevQuestId;
[ObservableProperty] private string? _selectedPageNextQuestId;

// --- Wizard (pattern Fernand) ---
[ObservableProperty] private string _questWizardPrefix = "quest_";
[ObservableProperty] private int _questWizardNumber = 1;
[ObservableProperty] private string _questWizardTitle = "";
[ObservableProperty] private string? _questWizardRequiredItemId;
[ObservableProperty] private int _questWizardRequiredQty = 200;
[ObservableProperty] private int _questWizardReputation = 12;
[ObservableProperty] private string _questWizardReputationTag = "Quete";
[ObservableProperty] private string _questWizardContent = "";
[ObservableProperty] private string _wizardStatusHint = "";



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

    
// --- Items catalog ---
public ObservableCollection<GameItemEntry> Items { get; } = new();
public ObservableCollection<GameItemEntry> VisibleItems { get; } = new();

    [ObservableProperty] private bool _isItemsLoading;
    [ObservableProperty] private string _itemsLoadingText = "Chargement des items…";

    public bool HasSelectedItem => SelectedItem is not null;

    public string ItemsStatusHint
        => "Source: HytaleGuide.net • Cache local: " + LocalCacheService.ItemsIndexPath;


[ObservableProperty] private string _itemSearch = "";
[ObservableProperty] private GameItemEntry? _selectedItem;
[ObservableProperty] private Bitmap? _selectedItemPreview;

partial void OnItemSearchChanged(string value) => ApplyItemFilter();

partial void OnSelectedItemChanged(GameItemEntry? value)
    {
        OnPropertyChanged(nameof(HasSelectedItem));
        _ = LoadSelectedItemPreviewAsync(value);
    }

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
    
        UpdateSelectedPageQuestLinks();
        DetectWizardDefaults();
    }

    partial void OnSelectedPageChanging(DialogPage? value) => SyncEditorListsToPage();

    partial void OnSelectedPageChanged(DialogPage? value)
    {
        LoadEditorListsFromPage();
    
        UpdateSelectedPageQuestLinks();
        DetectWizardDefaults();
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

    

    private void ApplyItemFilter()
    {
        var term = (ItemSearch ?? "").Trim();

        IEnumerable<GameItemEntry> list = string.IsNullOrWhiteSpace(term)
            ? Items
            : Items.Where(i =>
                (i.Name ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (i.Id ?? "").Contains(term, StringComparison.OrdinalIgnoreCase));

        VisibleItems.Clear();
        foreach (var it in list) VisibleItems.Add(it);

        if (SelectedItem is null || !VisibleItems.Contains(SelectedItem))
            SelectedItem = VisibleItems.FirstOrDefault();
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

    // --- Page editor list sync ---
    // Keep the editable ObservableCollections in sync with the SelectedPage.
    private void LoadEditorListsFromPage()
    {
        NextItemRequirements.Clear();
        NextCustomRequirements.Clear();
        Custom1ItemRequirements.Clear();
        Custom1CustomRequirements.Clear();
        Custom2ItemRequirements.Clear();
        Custom2CustomRequirements.Clear();
        Custom1CustomRewards.Clear();
        Custom1ConsoleCommands.Clear();
        Custom2CustomRewards.Clear();
        Custom2ConsoleCommands.Clear();

        if (SelectedPage is null) return;

        var nextReq = SelectedPage.NextButtonRequirement;
        if (nextReq?.ItemRequirements is not null)
            foreach (var r in nextReq.ItemRequirements) NextItemRequirements.Add(r);
        if (nextReq?.CustomRequirements is not null)
            foreach (var r in nextReq.CustomRequirements) NextCustomRequirements.Add(r);

        var c1Req = SelectedPage.CustomButton1ButtonRequirement;
        if (c1Req?.ItemRequirements is not null)
            foreach (var r in c1Req.ItemRequirements) Custom1ItemRequirements.Add(r);
        if (c1Req?.CustomRequirements is not null)
            foreach (var r in c1Req.CustomRequirements) Custom1CustomRequirements.Add(r);

        var c2Req = SelectedPage.CustomButton2ButtonRequirement;
        if (c2Req?.ItemRequirements is not null)
            foreach (var r in c2Req.ItemRequirements) Custom2ItemRequirements.Add(r);
        if (c2Req?.CustomRequirements is not null)
            foreach (var r in c2Req.CustomRequirements) Custom2CustomRequirements.Add(r);

        var c1Rew = SelectedPage.CustomButton1ButtonReward;
        if (c1Rew?.CustomRewards is not null)
            foreach (var r in c1Rew.CustomRewards) Custom1CustomRewards.Add(r);
        if (c1Rew?.ConsoleCommands is not null)
            foreach (var cmd in c1Rew.ConsoleCommands) Custom1ConsoleCommands.Add(new ConsoleCommandRow { Text = cmd ?? string.Empty });

        var c2Rew = SelectedPage.CustomButton2ButtonReward;
        if (c2Rew?.CustomRewards is not null)
            foreach (var r in c2Rew.CustomRewards) Custom2CustomRewards.Add(r);
        if (c2Rew?.ConsoleCommands is not null)
            foreach (var cmd in c2Rew.ConsoleCommands) Custom2ConsoleCommands.Add(new ConsoleCommandRow { Text = cmd ?? string.Empty });
    }

    private void SyncEditorListsToPage()
    {
        if (SelectedPage is null) return;

        // Next button requirements
        if (NextItemRequirements.Count == 0 && NextCustomRequirements.Count == 0)
        {
            SelectedPage.NextButtonRequirement = null;
        }
        else
        {
            SelectedPage.NextButtonRequirement ??= new ButtonRequirement();
            SelectedPage.NextButtonRequirement.ItemRequirements = NextItemRequirements.Count == 0 ? null : NextItemRequirements.ToList();
            SelectedPage.NextButtonRequirement.CustomRequirements = NextCustomRequirements.Count == 0 ? null : NextCustomRequirements.ToList();
        }

        // Custom1 requirements
        if (Custom1ItemRequirements.Count == 0 && Custom1CustomRequirements.Count == 0)
        {
            SelectedPage.CustomButton1ButtonRequirement = null;
        }
        else
        {
            SelectedPage.CustomButton1ButtonRequirement ??= new ButtonRequirement();
            SelectedPage.CustomButton1ButtonRequirement.ItemRequirements = Custom1ItemRequirements.Count == 0 ? null : Custom1ItemRequirements.ToList();
            SelectedPage.CustomButton1ButtonRequirement.CustomRequirements = Custom1CustomRequirements.Count == 0 ? null : Custom1CustomRequirements.ToList();
        }

        // Custom2 requirements
        if (Custom2ItemRequirements.Count == 0 && Custom2CustomRequirements.Count == 0)
        {
            SelectedPage.CustomButton2ButtonRequirement = null;
        }
        else
        {
            SelectedPage.CustomButton2ButtonRequirement ??= new ButtonRequirement();
            SelectedPage.CustomButton2ButtonRequirement.ItemRequirements = Custom2ItemRequirements.Count == 0 ? null : Custom2ItemRequirements.ToList();
            SelectedPage.CustomButton2ButtonRequirement.CustomRequirements = Custom2CustomRequirements.Count == 0 ? null : Custom2CustomRequirements.ToList();
        }

        // Custom1 rewards
        var c1Cmds = Custom1ConsoleCommands
            .Select(c => (c.Text ?? string.Empty).Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        if (Custom1CustomRewards.Count == 0 && c1Cmds.Count == 0)
        {
            SelectedPage.CustomButton1ButtonReward = null;
        }
        else
        {
            SelectedPage.CustomButton1ButtonReward ??= new ButtonReward();
            SelectedPage.CustomButton1ButtonReward.CustomRewards = Custom1CustomRewards.Count == 0 ? null : Custom1CustomRewards.ToList();
            SelectedPage.CustomButton1ButtonReward.ConsoleCommands = c1Cmds.Count == 0 ? null : c1Cmds;
        }

        // Custom2 rewards
        var c2Cmds = Custom2ConsoleCommands
            .Select(c => (c.Text ?? string.Empty).Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        if (Custom2CustomRewards.Count == 0 && c2Cmds.Count == 0)
        {
            SelectedPage.CustomButton2ButtonReward = null;
        }
        else
        {
            SelectedPage.CustomButton2ButtonReward ??= new ButtonReward();
            SelectedPage.CustomButton2ButtonReward.CustomRewards = Custom2CustomRewards.Count == 0 ? null : Custom2CustomRewards.ToList();
            SelectedPage.CustomButton2ButtonReward.ConsoleCommands = c2Cmds.Count == 0 ? null : c2Cmds;
        }
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

    public void DeleteSelectedNpc()
{
    if (SelectedNpc is null) return;

    var npc = SelectedNpc;

    // remove from main list
    Npcs.Remove(npc);

    ApplyNpcFilter();
    SelectedNpc = VisibleNpcs.FirstOrDefault();
    ApplyPageFilter();
    SelectedPage = VisiblePages.FirstOrDefault();

    Bundle.Npcs = Npcs.ToList();
    RefreshQuestIds();

    SetInfo("NPC supprimé.", title: "Quêtes");
}


// --- Helpers for InfoBar ---

    public void SetInfo(string message, string? title = null, InfoBarSeverity severity = InfoBarSeverity.Informational)
    {
        InfoTitle = title ?? "Info";
        InfoSeverity = severity;
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

    // --- Items catalog (file + web + cache)

    public void LoadItemsFromFile(string path)
    {
        var list = ItemsCatalogService.LoadFromJsonFile(path);

        Items.Clear();
        foreach (var it in list)
            Items.Add(it);

        ApplyItemFilter();
        _ = LoadSelectedItemPreviewAsync(SelectedItem);

        SetInfo($"Items importés: {Items.Count}", title: "Items");
    }

    public async Task LoadItemsFromCacheAsync()
    {
        IsItemsLoading = true;
        ItemsLoadingText = "Chargement du cache local…";

        try
        {
            if (!LocalCacheService.TryLoadItemsIndex(out var list))
            {
                // No cache => fetch once
                await RefreshItemsFromWebAsync();
                return;
            }

            Items.Clear();
            foreach (var it in list) Items.Add(it);

            ApplyItemFilter();
            await LoadSelectedItemPreviewAsync(SelectedItem);

            SetInfo($"Cache chargé: {Items.Count} items.", title: "Items");
        }
        finally
        {
            IsItemsLoading = false;
        }
    }

    public async Task RefreshItemsFromWebAsync()
    {
        IsItemsLoading = true;
        ItemsLoadingText = "Chargement du catalogue (local)…";

        try
        {
            // Hardcoded for now: load a bundled index (Assets/items.generated.json).
            // If missing, fall back to the bundled HTML (Assets/item-ids.html), then web downloader.
            var list = await HytaleGuideItemsService.LoadBundledAsync();
            if (list.Count == 0)
                list = await HytaleGuideItemsService.DownloadAsync();
            if (list.Count == 0)
            {
                SetError("Impossible de récupérer la liste d'items.", title: "Items");
                return;
            }

            LocalCacheService.SaveItemsIndex(list);

            Items.Clear();
            foreach (var it in list) Items.Add(it);

            ApplyItemFilter();
            await LoadSelectedItemPreviewAsync(SelectedItem);

            SetInfo($"Mis à jour: {Items.Count} items.", title: "Items");
        }
        finally
        {
            IsItemsLoading = false;
        }
    }

    public async Task EnsureItemThumbnailAsync(GameItemEntry? item)
    {
        if (item is null || item.Thumbnail is not null)
            return;

        try
        {
            // Use cached file if possible; download lazily if needed
            var path = item.LocalImagePath;
            if (string.IsNullOrWhiteSpace(path))
                path = await ItemImageCacheService.GetOrDownloadImagePathAsync(item);

            if (string.IsNullOrWhiteSpace(path))
                return;

            var bmp = await ItemImageCacheService.LoadBitmapAsync(path, decodeWidth: 64);
            if (bmp is null) return;

            await Dispatcher.UIThread.InvokeAsync(() => item.Thumbnail = bmp);
        }
        catch
        {
            // ignore thumbnail failures
        }
    }

    private async Task LoadSelectedItemPreviewAsync(GameItemEntry? item)
    {
        if (item is null)
        {
            SelectedItemPreview = null;
            return;
        }

        try
        {
            var path = item.LocalImagePath;
            if (string.IsNullOrWhiteSpace(path))
                path = await ItemImageCacheService.GetOrDownloadImagePathAsync(item);

            if (string.IsNullOrWhiteSpace(path))
            {
                SelectedItemPreview = null;
                return;
            }

            var bmp = await ItemImageCacheService.LoadBitmapAsync(path, decodeWidth: 520);
            SelectedItemPreview = bmp;
        }
        catch
        {
            SelectedItemPreview = null;
        }
    }


    // ----------------------------
    // Quest chain detection + Fernand pattern helpers
    // ----------------------------
    private static readonly Regex QuestIdRx = new(@"\bquest:[a-z_]+:([a-zA-Z0-9_\-]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static bool TryExtractQuestId(string? text, out string questId)
    {
        questId = string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var m = QuestIdRx.Match(text);
        if (!m.Success || m.Groups.Count < 2) return false;

        questId = m.Groups[1].Value;
        return !string.IsNullOrWhiteSpace(questId);
    }

    private static string? TryExtractSetStatusNextQuestId(ButtonReward? reward)
    {
        var list = reward?.CustomRewards;
        if (list is null) return null;

        foreach (var cr in list)
        {
            var id = cr?.RewardId;
            if (string.IsNullOrWhiteSpace(id)) continue;

            // quest:set_status:<questId>:AVAILABLE
            if (id.StartsWith("quest:set_status:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = id.Split(':', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4)
                    return parts[2];
            }
        }

        return null;
    }

    private static string? DetectQuestIdFromPage(DialogPage? page)
    {
        if (page is null) return null;

        // Prefer the explicit next button requirement: quest:completed:<id>
        var nextReqs = page.NextButtonRequirement?.CustomRequirements;
        if (nextReqs is not null)
        {
            foreach (var r in nextReqs)
                if (TryExtractQuestId(r?.RequirementId, out var id))
                    return id;
        }

        // Then: custom2 requirement often has quest:active:<id>
        var c2Reqs = page.CustomButton2ButtonRequirement?.CustomRequirements;
        if (c2Reqs is not null)
        {
            foreach (var r in c2Reqs)
                if (TryExtractQuestId(r?.RequirementId, out var id))
                    return id;
        }

        // Finally: rewards (quest:start / quest:complete / etc.)
        var rewards = page.CustomButton1ButtonReward?.CustomRewards;
        if (rewards is not null)
        {
            foreach (var rw in rewards)
                if (TryExtractQuestId(rw?.RewardId, out var id))
                    return id;
        }

        rewards = page.CustomButton2ButtonReward?.CustomRewards;
        if (rewards is not null)
        {
            foreach (var rw in rewards)
                if (TryExtractQuestId(rw?.RewardId, out var id))
                    return id;
        }

        return null;
    }

    private static bool TryParsePrefixAndNumber(string questId, out string prefix, out int number)
    {
        prefix = string.Empty;
        number = 0;

        // expected: something_<number>
        var idx = questId.LastIndexOf('_');
        if (idx <= 0 || idx >= questId.Length - 1) return false;

        var tail = questId[(idx + 1)..];
        if (!int.TryParse(tail, out number)) return false;

        prefix = questId[..(idx + 1)];
        return true;
    }

    private void UpdateSelectedPageQuestLinks()
    {
        var npc = SelectedNpc;
        var page = SelectedPage;

        var questId = DetectQuestIdFromPage(page);
        SelectedPageQuestId = questId;

        SelectedPageNextQuestId = TryExtractSetStatusNextQuestId(page?.CustomButton2ButtonReward);

        if (npc is null || string.IsNullOrWhiteSpace(questId))
        {
            SelectedPagePrevQuestId = null;
            return;
        }

        // Find page that sets this quest available
        string? prevQuestId = null;
        foreach (var p in npc.Pages)
        {
            var next = TryExtractSetStatusNextQuestId(p.CustomButton2ButtonReward);
            if (!string.IsNullOrWhiteSpace(next) &&
                next.Equals(questId, StringComparison.OrdinalIgnoreCase))
            {
                prevQuestId = DetectQuestIdFromPage(p);
                break;
            }
        }

        SelectedPagePrevQuestId = prevQuestId;
    }

    private IReadOnlyList<string> GetQuestIdsForNpc(NpcDef npc)
    {
        var set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        void add(string? text)
        {
            if (!TryExtractQuestId(text, out var id)) return;
            set.Add(id);
        }

        foreach (var p in npc.Pages)
        {
            if (p.NextButtonRequirement?.CustomRequirements is not null)
                foreach (var r in p.NextButtonRequirement.CustomRequirements) add(r.RequirementId);

            if (p.CustomButton1ButtonRequirement?.CustomRequirements is not null)
                foreach (var r in p.CustomButton1ButtonRequirement.CustomRequirements) add(r.RequirementId);

            if (p.CustomButton2ButtonRequirement?.CustomRequirements is not null)
                foreach (var r in p.CustomButton2ButtonRequirement.CustomRequirements) add(r.RequirementId);

            if (p.CustomButton1ButtonReward?.CustomRewards is not null)
                foreach (var rw in p.CustomButton1ButtonReward.CustomRewards) add(rw.RewardId);

            if (p.CustomButton2ButtonReward?.CustomRewards is not null)
                foreach (var rw in p.CustomButton2ButtonReward.CustomRewards) add(rw.RewardId);
        }

        return set.ToList();
    }

    private void DetectWizardDefaults()
    {
        var npc = SelectedNpc;

        if (npc is null)
        {
            WizardStatusHint = "Sélectionne un NPC pour utiliser le wizard.";
            return;
        }

        var allIds = GetQuestIdsForNpc(npc);
        var suggestedPrefix = "quest_";
        var max = 0;

        // Prefer prefix from current page, if any
        if (!string.IsNullOrWhiteSpace(SelectedPageQuestId) &&
            TryParsePrefixAndNumber(SelectedPageQuestId!, out var pfx, out _))
        {
            suggestedPrefix = pfx;
        }
        else
        {
            // Most common prefix in this NPC
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var id in allIds)
            {
                if (!TryParsePrefixAndNumber(id, out var p, out _)) continue;
                counts[p] = counts.TryGetValue(p, out var c) ? c + 1 : 1;
            }

            if (counts.Count > 0)
                suggestedPrefix = counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key.Length).First().Key;
        }

        foreach (var id in allIds)
        {
            if (!TryParsePrefixAndNumber(id, out var p, out var n)) continue;
            if (!p.Equals(suggestedPrefix, StringComparison.OrdinalIgnoreCase)) continue;
            if (n > max) max = n;
        }

        QuestWizardPrefix = suggestedPrefix;
        QuestWizardNumber = Math.Max(1, max + 1);

        if (string.IsNullOrWhiteSpace(QuestWizardReputationTag))
            QuestWizardReputationTag = $"Quete{(npc.Name ?? "Npc").Replace(" ", "")}{QuestWizardNumber}";

        WizardStatusHint = $"Détecté: prefix = {QuestWizardPrefix} | prochain num = {QuestWizardNumber} (max existant: {max}).";
    }

    private static void EnsureCustomReq(ButtonRequirement req, string requirementId)
    {
        req.CustomRequirements ??= new();
        if (!req.CustomRequirements.Any(x => (x.RequirementId ?? "").Equals(requirementId, StringComparison.OrdinalIgnoreCase)))
            req.CustomRequirements.Add(new CustomRequirement { RequirementId = requirementId });
    }

    private static void EnsureCustomReward(ButtonReward rew, string rewardId)
    {
        rew.CustomRewards ??= new();
        if (!rew.CustomRewards.Any(x => (x.RewardId ?? "").Equals(rewardId, StringComparison.OrdinalIgnoreCase)))
            rew.CustomRewards.Add(new CustomReward { RewardId = rewardId });
    }

    private static void RemoveCustomRewardsByPrefix(ButtonReward rew, string prefix)
    {
        if (rew.CustomRewards is null) return;
        rew.CustomRewards.RemoveAll(r => (r.RewardId ?? "").StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static void UpsertSetStatusReward(ButtonReward rew, string nextQuestId)
    {
        rew.CustomRewards ??= new();

        // remove any existing set_status
        rew.CustomRewards.RemoveAll(r => (r.RewardId ?? "").StartsWith("quest:set_status:", StringComparison.OrdinalIgnoreCase));

        rew.CustomRewards.Add(new CustomReward { RewardId = $"quest:set_status:{nextQuestId}:AVAILABLE" });
    }

    private void ApplyFernandPatternToPage(DialogPage page, string questId, string? nextQuestId, bool isFirstQuest)
    {
        // Ensure base button texts
        page.PreviousButtonText ??= "Précédent";
        page.NextButtonText ??= "Suivant ";
        page.CustomButton1Text ??= "Accepter";
        page.CustomButton2Text ??= "Donner";
        page.EnablePreviousButton = true;
        page.EnableCustomButton1 = true;
        page.EnableCustomButton2 = true;

        // Requirements
        page.NextButtonRequirement ??= new ButtonRequirement();
        EnsureCustomReq(page.NextButtonRequirement, $"quest:completed:{questId}");

        page.CustomButton2ButtonRequirement ??= new ButtonRequirement();
        EnsureCustomReq(page.CustomButton2ButtonRequirement, $"quest:active:{questId}");

        // Rewards: Accept
        page.CustomButton1ButtonReward ??= new ButtonReward();
        EnsureCustomReward(page.CustomButton1ButtonReward, $"quest:start:{questId}");
        if (isFirstQuest)
            EnsureCustomReward(page.CustomButton1ButtonReward, $"quest:status:{questId}:AVAILABLE");

        // Rewards: Give
        page.CustomButton2ButtonReward ??= new ButtonReward();
        EnsureCustomReward(page.CustomButton2ButtonReward, $"quest:complete:{questId}");

        if (!string.IsNullOrWhiteSpace(nextQuestId))
            UpsertSetStatusReward(page.CustomButton2ButtonReward, nextQuestId!);
        else
            RemoveCustomRewardsByPrefix(page.CustomButton2ButtonReward, "quest:set_status:");
    }

    private DialogPage? FindPageByQuestId(NpcDef npc, string questId)
    {
        foreach (var p in npc.Pages)
        {
            var id = DetectQuestIdFromPage(p);
            if (!string.IsNullOrWhiteSpace(id) && id.Equals(questId, StringComparison.OrdinalIgnoreCase))
                return p;
        }
        return null;
    }

    // ----------------------------
    // Commands (Wizard / Pattern)
    // ----------------------------
    [RelayCommand]
    private void WizardDetectDefaults()
    {
        UpdateSelectedPageQuestLinks();
        DetectWizardDefaults();
    }

    [RelayCommand]
    private void ApplyFernandPatternToSelectedPage()
    {
        if (SelectedNpc is null || SelectedPage is null)
        {
            SetInfo("Sélectionne un NPC et une page.", "Wizard");
            return;
        }

        var questId = DetectQuestIdFromPage(SelectedPage);
        if (string.IsNullOrWhiteSpace(questId))
        {
            SetInfo("Aucun questId détecté sur cette page (quest:*).", "Wizard", InfoBarSeverity.Warning);
            return;
        }

        // best effort: next quest from existing set_status OR next number
        string? nextQuestId = TryExtractSetStatusNextQuestId(SelectedPage.CustomButton2ButtonReward);
        if (string.IsNullOrWhiteSpace(nextQuestId) && TryParsePrefixAndNumber(questId!, out var pfx, out var n))
        {
            var candidate = $"{pfx}{n + 1}";
            if (FindPageByQuestId(SelectedNpc, candidate) is not null)
                nextQuestId = candidate;
        }

        var prevQuestId = SelectedPagePrevQuestId;
        var isFirst = string.IsNullOrWhiteSpace(prevQuestId);

        ApplyFernandPatternToPage(SelectedPage, questId!, nextQuestId, isFirst);

        RefreshQuestIds();
        LoadEditorListsFromPage();
        UpdateSelectedPageQuestLinks();

        SetInfo("Pattern Fernand appliqué sur la page sélectionnée.", "Wizard");
    }

    [RelayCommand]
    private void WizardCreateNextQuest()
    {
        if (SelectedNpc is null)
        {
            SetInfo("Sélectionne un NPC.", "Wizard", InfoBarSeverity.Warning);
            return;
        }

        var prefix = (QuestWizardPrefix ?? "").Trim();
        if (string.IsNullOrWhiteSpace(prefix))
        {
            SetInfo("Prefix invalide.", "Wizard", InfoBarSeverity.Warning);
            return;
        }

        var n = QuestWizardNumber;
        if (n <= 0) n = 1;

        var questId = $"{prefix}{n}";

        if (FindPageByQuestId(SelectedNpc, questId) is not null)
        {
            SetInfo($"La quête existe déjà: {questId}", "Wizard", InfoBarSeverity.Warning);
            return;
        }

        // link previous (if any)
        DialogPage? prevPage = null;
        if (n > 1)
            prevPage = FindPageByQuestId(SelectedNpc, $"{prefix}{n - 1}");

        // Build page
        var page = new DialogPage
        {
            PageId = Guid.NewGuid().ToString(),
            Title = $"Quête {n} : {(string.IsNullOrWhiteSpace(QuestWizardTitle) ? questId : QuestWizardTitle)}",
        };

        // content (auto if empty)
        var content = (QuestWizardContent ?? "").Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            var itemName = QuestWizardRequiredItemId;
            if (!string.IsNullOrWhiteSpace(itemName))
            {
                var item = Items.FirstOrDefault(i => (i.Id ?? "").Equals(itemName, StringComparison.OrdinalIgnoreCase));
                if (item is not null && !string.IsNullOrWhiteSpace(item.Name))
                    itemName = item.Name;
            }

            var qty = QuestWizardRequiredQty > 0 ? QuestWizardRequiredQty : 1;
            var rep = QuestWizardReputation;

            content =
$@"« J’ai besoin d’aide !

Apporte-moi {{#d4a017}}{{b}}{qty} {itemName}{{/}}{{/}}.

{(rep > 0 ? $"{{#6aa84f}}{{b}}Récompense : +{rep} réputation{{/}}{{/}}" : "")} »";
        }

        page.Content = content;

        // item requirement
        if (!string.IsNullOrWhiteSpace(QuestWizardRequiredItemId))
        {
            page.CustomButton2ButtonRequirement = new ButtonRequirement
            {
                ItemRequirements = new List<ItemRequirement>
                {
                    new() { ItemId = QuestWizardRequiredItemId, Quantity = Math.Max(1, QuestWizardRequiredQty) }
                },
                CustomRequirements = new List<CustomRequirement>()
            };
        }

        // Default structures so editor doesn't fight nulls
        page.NextButtonRequirement = new ButtonRequirement();
        page.CustomButton1ButtonReward = new ButtonReward();
        page.CustomButton2ButtonReward = new ButtonReward();

        // Console cmd for rep
        if (QuestWizardReputation > 0)
        {
            page.CustomButton2ButtonReward.ConsoleCommands = new List<string>
            {
                $"hyltrepadd @p {QuestWizardReputation} {(string.IsNullOrWhiteSpace(QuestWizardReputationTag) ? $"Quete{questId}" : QuestWizardReputationTag)}"
            };
        }

        // Apply Fernand pattern on this new page
        var isFirst = prevPage is null;
        ApplyFernandPatternToPage(page, questId, nextQuestId: null, isFirstQuest: isFirst);

        // Add page
        SelectedNpc.Pages.Add(page);

        // If NPC has no first page, set it
        if (string.IsNullOrWhiteSpace(SelectedNpc.FirstPageId))
            SelectedNpc.FirstPageId = page.PageId;

        // Link previous quest -> make this one available
        if (prevPage is not null)
        {
            prevPage.CustomButton2ButtonReward ??= new ButtonReward();
            EnsureCustomReward(prevPage.CustomButton2ButtonReward, $"quest:complete:{prefix}{n - 1}");
            UpsertSetStatusReward(prevPage.CustomButton2ButtonReward, questId);

            // keep previous page "Donner" requirements at least quest:active
            prevPage.CustomButton2ButtonRequirement ??= new ButtonRequirement();
            EnsureCustomReq(prevPage.CustomButton2ButtonRequirement, $"quest:active:{prefix}{n - 1}");
            prevPage.NextButtonRequirement ??= new ButtonRequirement();
            EnsureCustomReq(prevPage.NextButtonRequirement, $"quest:completed:{prefix}{n - 1}");
        }

        ApplyPageFilter();
        RefreshQuestIds();

        SelectedPage = page;

        SetInfo($"Quête créée: {questId} (pageId: {page.PageId})", "Wizard");
        DetectWizardDefaults();
    }

    [RelayCommand]
    private void WizardRepairChain()
    {
        if (SelectedNpc is null)
        {
            SetInfo("Sélectionne un NPC.", "Wizard", InfoBarSeverity.Warning);
            return;
        }

        var prefix = (QuestWizardPrefix ?? "").Trim();
        if (string.IsNullOrWhiteSpace(prefix))
        {
            SetInfo("Prefix invalide.", "Wizard", InfoBarSeverity.Warning);
            return;
        }

        // Build map questId -> page
        var map = new SortedDictionary<int, (string questId, DialogPage page)>();
        foreach (var p in SelectedNpc.Pages)
        {
            var qid = DetectQuestIdFromPage(p);
            if (string.IsNullOrWhiteSpace(qid)) continue;
            if (!TryParsePrefixAndNumber(qid!, out var pfx, out var num)) continue;
            if (!pfx.Equals(prefix, StringComparison.OrdinalIgnoreCase)) continue;

            map[num] = (qid!, p);
        }

        if (map.Count == 0)
        {
            SetInfo("Aucune quête détectée pour ce prefix sur ce NPC.", "Wizard", InfoBarSeverity.Warning);
            return;
        }

        var nums = map.Keys.OrderBy(x => x).ToList();
        foreach (var num in nums)
        {
            var (qid, page) = map[num];
            var nextQuestId = map.TryGetValue(num + 1, out var next) ? next.questId : null;
            var isFirst = !map.ContainsKey(num - 1);

            ApplyFernandPatternToPage(page, qid, nextQuestId, isFirst);

            // Ensure set_status is correct only if next exists
            if (nextQuestId is null)
            {
                page.CustomButton2ButtonReward ??= new ButtonReward();
                RemoveCustomRewardsByPrefix(page.CustomButton2ButtonReward, "quest:set_status:");
            }
        }

        RefreshQuestIds();
        LoadEditorListsFromPage();
        UpdateSelectedPageQuestLinks();

        SetInfo($"Chaîne réparée (pattern Fernand) pour prefix: {prefix}", "Wizard");
        DetectWizardDefaults();
    }}

public sealed partial class ConsoleCommandRow : ObservableObject
{
    [ObservableProperty] private string _text = string.Empty;
}

