using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hylterium.QuestStudio.Models;
using Hylterium.QuestStudio.Services;

namespace Hylterium.QuestStudio.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty] private string _status = "Prêt. Ouvre un JSON de NPC Dialog / Quests Maker.";
    [ObservableProperty] private string? _currentFilePath;

    public ObservableCollection<NpcDef> Npcs { get; } = new();

    [ObservableProperty] private NpcDef? _selectedNpc;
    [ObservableProperty] private DialogPage? _selectedPage;

    public ObservableCollection<string> QuestIds { get; } = new();

    public QuestBundle Bundle { get; private set; } = new();

    public void LoadBundle(QuestBundle bundle, string? filePath)
    {
        Bundle = bundle;
        CurrentFilePath = filePath;

        Npcs.Clear();
        foreach (var npc in bundle.Npcs) Npcs.Add(npc);

        SelectedNpc = Npcs.FirstOrDefault();
        SelectedPage = SelectedNpc?.Pages?.FirstOrDefault();

        RefreshQuestIds();

        Status = filePath is null ? "Bundle chargé." : $"Chargé: {filePath}";
    }

    public QuestBundle BuildBundleFromUi()
    {
        // We bind directly to models; so just rebuild the list
        Bundle.Npcs = Npcs.ToList();
        return Bundle;
    }

    public void RefreshQuestIds()
    {
        QuestIds.Clear();
        foreach (var id in QuestIdScanner.Scan(Bundle))
            QuestIds.Add(id);
    }

    partial void OnSelectedNpcChanged(NpcDef? value)
    {
        SelectedPage = value?.Pages?.FirstOrDefault();
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
        SelectedNpc = npc;
        SelectedPage = npc.Pages.FirstOrDefault();
        Status = "NPC ajouté (brouillon).";
        Bundle.Npcs = Npcs.ToList();
        RefreshQuestIds();
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
        SelectedPage = page;

        // set firstPageId if empty
        if (string.IsNullOrWhiteSpace(SelectedNpc.FirstPageId))
            SelectedNpc.FirstPageId = pageId;

        Status = $"Page ajoutée: {pageId}";
        Bundle.Npcs = Npcs.ToList();
        RefreshQuestIds();
    }

    [RelayCommand]
    private void DeleteSelectedPage()
    {
        if (SelectedNpc is null || SelectedPage is null) return;
        var idx = SelectedNpc.Pages.IndexOf(SelectedPage);
        if (idx < 0) return;
        SelectedNpc.Pages.RemoveAt(idx);
        SelectedPage = SelectedNpc.Pages.FirstOrDefault();

        Status = "Page supprimée.";
        Bundle.Npcs = Npcs.ToList();
        RefreshQuestIds();
    }
}
