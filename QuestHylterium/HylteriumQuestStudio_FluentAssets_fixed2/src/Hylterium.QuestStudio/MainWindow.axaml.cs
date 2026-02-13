using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Hylterium.QuestStudio.Services;
using Hylterium.QuestStudio.ViewModels;

namespace Hylterium.QuestStudio;

public partial class MainWindow : Window
{
    private MainWindowViewModel VM => (MainWindowViewModel)DataContext!;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }

    private async void OnOpenClick(object? sender, RoutedEventArgs e)
        => await OpenQuestJsonAsync();

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
        => await SaveQuestJsonAsync(saveAs: false);

    private async void OnSaveAsClick(object? sender, RoutedEventArgs e)
        => await SaveQuestJsonAsync(saveAs: true);

    private async void OnScanHytaleModsClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var count = VM.ScanDefaultHytaleMods();
            VM.SetInfo($"Mods scannés. Packs ajoutés: {count}");
        }
        catch (Exception ex)
        {
            VM.SetError("Erreur scan mods: " + ex.Message);
        }
    }

    private async void OnAddAssetPackClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Ajouter un pack assets (zip)",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Archive") { Patterns = new[] { "*.zip" } },
                FilePickerFileTypes.All
            }
        });

        if (files.Count == 0) return;

        var path = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;

        try
        {
            VM.AddAssetPack(path);
            VM.SetInfo($"Pack ajouté: {Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            VM.SetError("Erreur ajout pack: " + ex.Message);
        }
    }

    private async void OnAddAssetFolderClick(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Ajouter un dossier d'assets",
            AllowMultiple = false
        });

        if (folders.Count == 0) return;

        var path = folders[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;

        try
        {
            VM.AddAssetPack(path);
            VM.SetInfo($"Dossier ajouté: {Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            VM.SetError("Erreur ajout dossier: " + ex.Message);
        }
    }

    private async void OnCopySelectedAssetPathClick(object? sender, RoutedEventArgs e)
    {
        if (VM.SelectedAsset is null) return;
        var path = VM.SelectedAsset.RelativePath;

        var top = TopLevel.GetTopLevel(this);
        if (top?.Clipboard is null) return;

        await top.Clipboard.SetTextAsync(path);
        VM.SetInfo("Chemin copié dans le presse-papiers.");
    }

    private void OnAssignSelectedAssetToNpcEntityIdClick(object? sender, RoutedEventArgs e)
    {
        if (VM.SelectedNpc is null || VM.SelectedAsset is null) return;
        VM.SelectedNpc.EntityId = VM.SelectedAsset.RelativePath;
        VM.SetInfo("entityId du NPC mis à jour (tu peux ajuster au besoin).", title: "Assignation");
    }

    private async Task OpenQuestJsonAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Ouvrir un JSON de quêtes",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("JSON") { Patterns = new[] { "*.json" } },
                FilePickerFileTypes.All
            }
        });

        if (files.Count == 0) return;

        var path = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            var bundle = QuestJson.LoadFromFile(path);
            VM.LoadBundle(bundle, path);
        }
        catch (Exception ex)
        {
            VM.SetError("Erreur ouverture: " + ex.Message);
        }
    }

    private async Task SaveQuestJsonAsync(bool saveAs)
    {
        var path = VM.CurrentFilePath;

        if (saveAs || string.IsNullOrWhiteSpace(path))
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Sauver le JSON",
                DefaultExtension = "json",
                SuggestedFileName = "quests.json",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("JSON") { Patterns = new[] { "*.json" } }
                }
            });

            path = file?.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path)) return;

            VM.CurrentFilePath = path;
        }

        try
        {
            var bundle = VM.BuildBundleFromUi();
            QuestJson.SaveToFile(path!, bundle);
            VM.SetInfo("Sauvé: " + path);
            VM.RefreshQuestIds();
        }
        catch (Exception ex)
        {
            VM.SetError("Erreur sauvegarde: " + ex.Message);
        }
    }
}
