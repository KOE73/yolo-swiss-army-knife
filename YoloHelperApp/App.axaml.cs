using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using YoloHelperApp.ViewModels;
using YoloHelperApp.Views;

namespace YoloHelperApp;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainVm = new MainWindowViewModel(desktop.Args);
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainVm,
            };
            // Don't leave orphaned yolo train/export processes behind
            desktop.Exit += (_, _) => mainVm.Shutdown();
        }

        base.OnFrameworkInitializationCompleted();
    }
}