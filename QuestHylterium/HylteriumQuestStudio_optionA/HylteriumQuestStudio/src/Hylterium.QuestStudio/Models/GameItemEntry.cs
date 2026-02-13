using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Hylterium.QuestStudio.Models;

public partial class GameItemEntry : ObservableObject
{
    [ObservableProperty] private string _id = string.Empty;
    [ObservableProperty] private string _name = string.Empty;

    // Optional: comes from https://hytaleguide.net/item-ids
    [ObservableProperty] private string? _imageUrl;

    // Local cache path for downloaded icon
    [ObservableProperty] private string? _localImagePath;

    // Small thumbnail for list UI (lazy loaded)
    [ObservableProperty] private Bitmap? _thumbnail;
}
