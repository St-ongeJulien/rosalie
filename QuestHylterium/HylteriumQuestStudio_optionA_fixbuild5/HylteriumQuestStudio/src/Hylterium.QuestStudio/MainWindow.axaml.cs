using System;
using System.IO;
using System.Threading.Tasks;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;
using Hylterium.QuestStudio.Models;
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

        if (DataContext is INotifyPropertyChanged inpc)
        {
            inpc.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName is nameof(MainWindowViewModel.SelectedPage) or nameof(MainWindowViewModel.SelectedNpc))
                    UpdateContentPreviewFromVm();
            };
        }

        UpdateContentPreviewFromVm();
    }

    private async void OnOpenClick(object? sender, RoutedEventArgs e)
        => await OpenQuestJsonAsync();

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
        => await SaveQuestJsonAsync(saveAs: false);

    private async void OnSaveAsClick(object? sender, RoutedEventArgs e)
        => await SaveQuestJsonAsync(saveAs: true);

    private async void OnDeleteNpcClick(object? sender, RoutedEventArgs e)
    {
        if (VM.SelectedNpc is null)
            return;

        var npcName = string.IsNullOrWhiteSpace(VM.SelectedNpc.Name) ? "(sans nom)" : VM.SelectedNpc.Name;

        var dlg = new ContentDialog
        {
            Title = "Supprimer le NPC ?",
            Content = $"Tu es sur le point de supprimer: {npcName}\n\nCette action est irréversible (dans le fichier).",
            PrimaryButtonText = "Supprimer",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Close
        };

        var result = await dlg.ShowAsync(this);
        if (result == ContentDialogResult.Primary)
            VM.DeleteSelectedNpc();
    }

    private void OnAssignSelectedAssetToNpcEntityIdClick(object? sender, RoutedEventArgs e)
    {
        if (VM.SelectedNpc is null || VM.SelectedAsset is null)
            return;

        VM.SelectedNpc.EntityId = VM.SelectedAsset.RelativePath;
        VM.SetInfo("entityId du NPC mis à jour (tu peux ajuster au besoin).", title: "Assignation");
    }

    private async void OnCopySelectedAssetPathClick(object? sender, RoutedEventArgs e)
    {
        if (VM.SelectedAsset is null)
            return;

        var top = TopLevel.GetTopLevel(this);
        if (top?.Clipboard is null)
            return;

        await top.Clipboard.SetTextAsync(VM.SelectedAsset.RelativePath);
        VM.SetInfo("Chemin copié dans le presse-papiers.");
    }

    private void OnScanHytaleModsClick(object? sender, RoutedEventArgs e)
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

        if (files.Count == 0)
            return;

        var path = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

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

        if (folders.Count == 0)
            return;

        var path = folders[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return;

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

    // ----------------------------
    // Items tab
    // ----------------------------

    private async void OnLoadItemsClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            await VM.LoadItemsFromCacheAsync();
        }
        catch (Exception ex)
        {
            VM.SetError(ex.Message, title: "Items");
        }
    }

    private async void OnRefreshItemsFromWebClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            await VM.RefreshItemsFromWebAsync();
        }
        catch (Exception ex)
        {
            VM.SetError(ex.Message, title: "Items");
        }
    }

    private async void OnImportItemsClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Importer un catalogue d'items (items.json ou .txt)",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("JSON") { Patterns = new[] { "*.json" } },
                new FilePickerFileType("Text") { Patterns = new[] { "*.txt" } },
                FilePickerFileTypes.All
            }
        });

        if (files.Count == 0)
            return;

        var path = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            VM.LoadItemsFromFile(path);
        }
        catch (Exception ex)
        {
            VM.SetError("Erreur import items: " + ex.Message, title: "Items");
        }
    }

    private async void OnCopySelectedItemIdClick(object? sender, RoutedEventArgs e)
    {
        if (VM.SelectedItem is null)
            return;

        var top = TopLevel.GetTopLevel(this);
        if (top?.Clipboard is null)
            return;

        await top.Clipboard.SetTextAsync(VM.SelectedItem.Id);
        VM.SetInfo("itemId copié dans le presse-papiers.", title: "Items");
    }

    private async void OnCopySelectedItemJsonClick(object? sender, RoutedEventArgs e)
    {
        if (VM.SelectedItem is null)
            return;

        var top = TopLevel.GetTopLevel(this);
        if (top?.Clipboard is null)
            return;

        await top.Clipboard.SetTextAsync($"\"itemId\": \"{VM.SelectedItem.Id}\"");
        VM.SetInfo("Snippet JSON copié dans le presse‑papiers.", title: "Items");
    }

    private void OnItemRowAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is Control c && c.DataContext is GameItemEntry item)
            _ = VM.EnsureItemThumbnailAsync(item);
    }

    // ----------------------------
    // File open/save
    // ----------------------------

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

        if (files.Count == 0)
            return;

        var path = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
            return;

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
            if (string.IsNullOrWhiteSpace(path))
                return;

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

// ----------------------------
// Rich text helper toolbar + live preview
// Supported tags: {b} {i} {#RRGGBB} {/}
// ----------------------------

private void UpdateContentPreviewFromVm()
{
    if (DataContext is not MainWindowViewModel vm) return;
    UpdateContentPreview(vm.SelectedPage?.Content);
}

private void OnPageContentTextChanged(object? sender, TextChangedEventArgs e)
{
    if (sender is TextBox tb)
        UpdateContentPreview(tb.Text);
    else
        UpdateContentPreviewFromVm();
}

private void OnFormatBoldClick(object? sender, RoutedEventArgs e) => WrapSelectionWithTag("{b}");
private void OnFormatItalicClick(object? sender, RoutedEventArgs e) => WrapSelectionWithTag("{i}");

private void OnFormatResetClick(object? sender, RoutedEventArgs e)
{
    InsertAtCaret("{/}");
    UpdateContentPreviewFromVm();
}

private void OnFormatColorPresetClick(object? sender, RoutedEventArgs e)
{
    if (sender is not Control c) return;
    var tag = c.Tag?.ToString();
    if (string.IsNullOrWhiteSpace(tag)) return;

    var hex = tag.Trim();
    if (!hex.StartsWith("#")) hex = "#" + hex;
    if (hex.Length != 7) return;

    WrapSelectionWithTag("{" + hex + "}");
    UpdateContentPreviewFromVm();
}

private void OnFormatApplyCustomColorClick(object? sender, RoutedEventArgs e)
{
    var tb = this.FindControl<TextBox>("CustomColorHexTextBox");
    var raw = tb?.Text?.Trim();
    if (string.IsNullOrWhiteSpace(raw)) return;

    var hex = raw!;
    if (!hex.StartsWith("#")) hex = "#" + hex;
    if (hex.Length != 7) return;

    WrapSelectionWithTag("{" + hex + "}");
    UpdateContentPreviewFromVm();
}

private void WrapSelectionWithTag(string openTag)
{
    var box = this.FindControl<TextBox>("PageContentTextBox");
    if (box is null) return;

    var text = box.Text ?? string.Empty;
    var start = Math.Min(box.SelectionStart, box.SelectionEnd);
    var end = Math.Max(box.SelectionStart, box.SelectionEnd);

    const string closeTag = "{/}";

    if (start == end)
    {
        // Insert open + close and place caret in-between
        var insert = openTag + closeTag;
        box.Text = text.Insert(start, insert);
        box.CaretIndex = start + openTag.Length;
        box.SelectionStart = box.CaretIndex;
        box.SelectionEnd = box.CaretIndex;
    }
    else
    {
        var selected = text.Substring(start, end - start);
        var replaced = openTag + selected + closeTag;

        box.Text = text.Substring(0, start) + replaced + text.Substring(end);

        box.SelectionStart = start + openTag.Length;
        box.SelectionEnd = start + openTag.Length + selected.Length;
        box.CaretIndex = box.SelectionEnd;
    }
}

private void InsertAtCaret(string toInsert)
{
    var box = this.FindControl<TextBox>("PageContentTextBox");
    if (box is null) return;

    var text = box.Text ?? string.Empty;
    var caret = box.CaretIndex;

    box.Text = text.Insert(caret, toInsert);
    box.CaretIndex = caret + toInsert.Length;
    box.SelectionStart = box.CaretIndex;
    box.SelectionEnd = box.CaretIndex;
}

private readonly record struct StyleState(bool Bold, bool Italic, IBrush? Foreground);

private void UpdateContentPreview(string? raw)
{
    var preview = this.FindControl<TextBlock>("PageContentPreview");
    if (preview is null) return;

	    // IMPORTANT: si Text contient encore quelque chose (ou est assigné par un style/binding),
	    // Avalonia peut ignorer les Inlines. On force Text vide pour garantir le rendu riche.
	    preview.Text = string.Empty;

    // IMPORTANT: sur certaines versions / styles, Inlines peut être null tant qu'on ne l'utilise pas.
    // On force l'accès direct pour garantir que les styles (bold/italic/couleur) s'affichent bien.
    var inlines = preview.Inlines;
    inlines.Clear();
    foreach (var inline in ParseRichTextTags(raw ?? string.Empty))
        inlines.Add(inline);
}

private static IEnumerable<Inline> ParseRichTextTags(string text)
{
    var stack = new Stack<StyleState>();
    var state = new StyleState(Bold: false, Italic: false, Foreground: null);

    void flushSegment(List<Inline> list, string seg)
    {
        // normalize CRLF
        seg = seg.Replace("\r\n", "\n").Replace('\r', '\n');

        var parts = seg.Split('\n');
        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length > 0)
            {
	        			var run = new Run(parts[i]);
	        			if (state.Bold) run.FontWeight = FontWeight.Bold;
	        			if (state.Italic) run.FontStyle = FontStyle.Italic;
	        			if (state.Foreground is not null) run.Foreground = state.Foreground;
	        			list.Add(run);
            }

            if (i < parts.Length - 1)
                list.Add(new LineBreak());
        }
    }

    var outList = new List<Inline>();
    var iPos = 0;

    while (iPos < text.Length)
    {
        var open = text.IndexOf('{', iPos);
        if (open < 0)
        {
            flushSegment(outList, text[iPos..]);
            break;
        }

        if (open > iPos)
            flushSegment(outList, text.Substring(iPos, open - iPos));

        var close = text.IndexOf('}', open + 1);
        if (close < 0)
        {
            // unmatched '{' -> literal
            flushSegment(outList, text[open..]);
            break;
        }

        var tag = text.Substring(open + 1, close - open - 1);

        if (tag.Equals("b", StringComparison.OrdinalIgnoreCase))
        {
            stack.Push(state);
            state = state with { Bold = true };
        }
        else if (tag.Equals("i", StringComparison.OrdinalIgnoreCase))
        {
            stack.Push(state);
            state = state with { Italic = true };
        }
        else if (tag.Equals("/", StringComparison.OrdinalIgnoreCase))
        {
            if (stack.Count > 0) state = stack.Pop();
        }
        else if (tag.StartsWith("#") && tag.Length == 7)
        {
            // {#RRGGBB}
            try
            {
                var color = Color.Parse(tag);
                stack.Push(state);
                state = state with { Foreground = new SolidColorBrush(color) };
            }
            catch
            {
                // ignore malformed color tags -> render literally
                flushSegment(outList, "{" + tag + "}");
            }
        }
        else
        {
            // unknown tag -> literal
            flushSegment(outList, "{" + tag + "}");
        }

        iPos = close + 1;
    }

    return outList;
}
}
