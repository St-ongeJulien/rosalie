using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
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

    private async void OnOpenClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => await OpenAsync();

    private async void OnSaveClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => await SaveAsync(saveAs: false);

    private async void OnSaveAsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => await SaveAsync(saveAs: true);

    private async Task OpenAsync()
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
            VM.Status = "Erreur ouverture: " + ex.Message;
        }
    }

    private async Task SaveAsync(bool saveAs)
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
            VM.Status = "Sauvé: " + path;
            VM.RefreshQuestIds();
        }
        catch (Exception ex)
        {
            VM.Status = "Erreur sauvegarde: " + ex.Message;
        }
    }
}
