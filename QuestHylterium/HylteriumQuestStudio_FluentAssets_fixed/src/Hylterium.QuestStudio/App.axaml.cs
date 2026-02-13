using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using FluentAvalonia.Styling;

namespace Hylterium.QuestStudio;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // Accent color (Hylterium purple)
        var faTheme = AvaloniaLocator.Current.GetService<FluentAvaloniaTheme>();
        if (faTheme is not null)
        {
            faTheme.PreferUserAccentColor = false;
            faTheme.CustomAccentColor = Color.Parse("#9F44D3");
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow();

        base.OnFrameworkInitializationCompleted();
    }
}
