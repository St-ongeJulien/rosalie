using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Hylterium.QuestStudio.ViewModels;
using Hylterium.QuestStudio.Models;

namespace Hylterium.QuestStudio.Controls;

public partial class ItemPicker : UserControl
{
    public ItemPicker()
    {
        InitializeComponent();

        Suggestions = new ObservableCollection<GameItemEntry>();

        var box = this.FindControl<TextBox>("SearchBox");
        if (box is not null)
        {
            box.GotFocus += (_, _) => OpenAndRefresh();
            box.LostFocus += (_, _) => CommitIfExactMatch();
            box.KeyDown += OnSearchKeyDown;
        }

        var list = this.FindControl<ListBox>("ResultsList");
        if (list is not null)
        {
            list.KeyDown += OnListKeyDown;
        }

        this.GetObservable(QueryProperty).Subscribe(new ActionObserver<string?>(_ => OpenAndRefresh()));
        this.GetObservable(ItemsSourceProperty).Subscribe(new ActionObserver<IEnumerable<GameItemEntry>?>(_ => OpenAndRefresh()));
        this.GetObservable(SelectedItemIdProperty).Subscribe(new ActionObserver<string?>(_ => SyncFromSelectedId()));
    }


    private sealed class ActionObserver<T> : IObserver<T>
    {
        private readonly Action<T> _onNext;
        public ActionObserver(Action<T> onNext) => _onNext = onNext;
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(T value) => _onNext(value);
    }

    // Items
    public static readonly StyledProperty<IEnumerable<GameItemEntry>?> ItemsSourceProperty =
        AvaloniaProperty.Register<ItemPicker, IEnumerable<GameItemEntry>?>(nameof(ItemsSource));

    public IEnumerable<GameItemEntry>? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    // Selected
    public static readonly StyledProperty<string?> SelectedItemIdProperty =
        AvaloniaProperty.Register<ItemPicker, string?>(nameof(SelectedItemId));

    public string? SelectedItemId
    {
        get => GetValue(SelectedItemIdProperty);
        set => SetValue(SelectedItemIdProperty, value);
    }

    // Query (search text)
    public static readonly StyledProperty<string?> QueryProperty =
        AvaloniaProperty.Register<ItemPicker, string?>(nameof(Query));

    public string? Query
    {
        get => GetValue(QueryProperty);
        set => SetValue(QueryProperty, value);
    }

    // Watermark
    public static readonly StyledProperty<string?> WatermarkProperty =
        AvaloniaProperty.Register<ItemPicker, string?>(nameof(Watermark), "Rechercher un item…");

    public string? Watermark
    {
        get => GetValue(WatermarkProperty);
        set => SetValue(WatermarkProperty, value);
    }

    // Max results
    public static readonly StyledProperty<int> MaxResultsProperty =
        AvaloniaProperty.Register<ItemPicker, int>(nameof(MaxResults), 12);

    public int MaxResults
    {
        get => GetValue(MaxResultsProperty);
        set => SetValue(MaxResultsProperty, value);
    }

    // Suggestions + thumbnail
    public ObservableCollection<GameItemEntry> Suggestions { get; }

    public static readonly StyledProperty<Avalonia.Media.Imaging.Bitmap?> SelectedThumbnailProperty =
        AvaloniaProperty.Register<ItemPicker, Avalonia.Media.Imaging.Bitmap?>(nameof(SelectedThumbnail));

    public Avalonia.Media.Imaging.Bitmap? SelectedThumbnail
    {
        get => GetValue(SelectedThumbnailProperty);
        set => SetValue(SelectedThumbnailProperty, value);
    }

    private void OpenAndRefresh()
    {
        RefreshSuggestions();

        var popup = this.FindControl<Popup>("ResultsPopup");
        if (popup is null)
            return;

        var q = (Query ?? "").Trim();
        popup.IsOpen = Suggestions.Count > 0 && (q.Length > 0 || (ItemsSource?.Any() ?? false));
    }

    private void ClosePopup()
    {
        var popup = this.FindControl<Popup>("ResultsPopup");
        if (popup is not null)
            popup.IsOpen = false;
    }

    private void RefreshSuggestions()
    {
        Suggestions.Clear();

        var items = ItemsSource?.ToList();
        if (items is null || items.Count == 0)
            return;

        var q = (Query ?? "").Trim();

        IEnumerable<GameItemEntry> best;
        if (string.IsNullOrWhiteSpace(q))
        {
            // Default: show a small alphabetical window (simple + predictable)
            best = items.OrderBy(i => i.Name ?? i.Id ?? string.Empty)
                        .ThenBy(i => i.Id ?? string.Empty)
                        .Take(MaxResults);
        }
        else
        {
            var term = q.ToLowerInvariant();
            best = items
                .Select(i => (item: i, score: ComputeScore(i, term)))
                .Where(t => t.score < 1_000_000)
                .OrderBy(t => t.score)
                .ThenBy(t => t.item.Name ?? t.item.Id ?? string.Empty)
                .Take(MaxResults)
                .Select(t => t.item);
        }

        foreach (var it in best)
            Suggestions.Add(it);
    }

    private static int ComputeScore(GameItemEntry it, string term)
    {
        var id = (it.Id ?? "").ToLowerInvariant();
        var name = (it.Name ?? "").ToLowerInvariant();

        if (id.Length == 0 && name.Length == 0)
            return 1_000_000;

        if (id.Equals(term, StringComparison.OrdinalIgnoreCase)) return -2000;
        if (name.Equals(term, StringComparison.OrdinalIgnoreCase)) return -1500;

        if (id.StartsWith(term)) return -1000 + (id.Length - term.Length);
        if (name.StartsWith(term)) return -800 + (name.Length - term.Length);

        var idxId = id.IndexOf(term, StringComparison.OrdinalIgnoreCase);
        if (idxId >= 0) return 0 + idxId;

        var idxName = name.IndexOf(term, StringComparison.OrdinalIgnoreCase);
        if (idxName >= 0) return 200 + idxName;

        // fallback: token match
        var tokens = term.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length > 1)
        {
            var ok = tokens.All(t => id.Contains(t) || name.Contains(t));
            if (ok) return 400;
        }

        return 1_000_000;
    }

    private void SyncFromSelectedId()
    {
        var id = (SelectedItemId ?? "").Trim();
        if (string.IsNullOrWhiteSpace(id))
        {
            SelectedThumbnail = null;
            return;
        }

        var item = ItemsSource?.FirstOrDefault(i => (i.Id ?? "").Equals(id, StringComparison.OrdinalIgnoreCase));
        if (item is null)
            return;

        // ensure thumb (best effort)
        var vm = GetMainVm();
        if (vm is not null)
            _ = vm.EnsureItemThumbnailAsync(item);

        SelectedThumbnail = item.Thumbnail;

        var box = this.FindControl<TextBox>("SearchBox");
        if (box is not null && !box.IsFocused)
            box.Text = !string.IsNullOrWhiteSpace(item.Name) ? item.Name : item.Id;
    }

    private void CommitIfExactMatch()
    {
        var q = (Query ?? "").Trim();
        if (string.IsNullOrWhiteSpace(q))
            return;

        var item = ItemsSource?.FirstOrDefault(i =>
            (i.Id ?? "").Equals(q, StringComparison.OrdinalIgnoreCase) ||
            (i.Name ?? "").Equals(q, StringComparison.OrdinalIgnoreCase));

        if (item is not null)
            SelectItem(item);
    }

    private void SelectItem(GameItemEntry item)
    {
        SelectedItemId = item.Id;
        Query = !string.IsNullOrWhiteSpace(item.Name) ? item.Name : item.Id;
        SelectedThumbnail = item.Thumbnail;

        var vm = GetMainVm();
        if (vm is not null)
            _ = vm.EnsureItemThumbnailAsync(item);

        ClosePopup();
    }

    private MainWindowViewModel? GetMainVm()
    {
        var win = this.FindAncestorOfType<Window>();
        return win?.DataContext as MainWindowViewModel;
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down)
        {
            var list = this.FindControl<ListBox>("ResultsList");
            if (list is not null && Suggestions.Count > 0)
            {
                list.SelectedIndex = Math.Max(0, list.SelectedIndex);
                list.Focus();
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Escape)
        {
            ClosePopup();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            // If a suggestion is highlighted, pick it.
            var list = this.FindControl<ListBox>("ResultsList");
            if (list?.SelectedItem is GameItemEntry it)
            {
                SelectItem(it);
                e.Handled = true;
                return;
            }

            CommitIfExactMatch();
            ClosePopup();
            e.Handled = true;
        }
    }

    private void OnListKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var list = this.FindControl<ListBox>("ResultsList");
            if (list?.SelectedItem is GameItemEntry it)
            {
                SelectItem(it);
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Escape)
        {
            ClosePopup();
            var box = this.FindControl<TextBox>("SearchBox");
            box?.Focus();
            e.Handled = true;
        }
    }

    // Called from XAML on PointerPressed to pick quickly.
    private void OnResultsPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var list = this.FindControl<ListBox>("ResultsList");
        if (list?.SelectedItem is GameItemEntry it)
            SelectItem(it);
    }

    private void OnSuggestionRowAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is Control c && c.DataContext is GameItemEntry item)
        {
            var vm = GetMainVm();
            if (vm is not null)
                _ = vm.EnsureItemThumbnailAsync(item);
        }
    }
}
