using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using YoloHelperApp.Services;
using YoloHelperApp.ViewModels;

namespace YoloHelperApp.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void OnOpenProjectClick(object sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open YSAK Project",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("YSAK Project") { Patterns = new[] { "*.ysak" } }
            }
        });

        if (files.Count > 0 && DataContext is MainWindowViewModel vm)
        {
            vm.OpenProject(files[0].Path.LocalPath);
        }
    }

    private async void OnCreateProjectClick(object sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select project folder (the .ysak is named after it)",
            AllowMultiple = false
        });

        if (folders.Count == 0 || DataContext is not MainWindowViewModel vm) return;

        string folder = folders[0].Path.LocalPath;
        string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(folder));
        if (string.IsNullOrWhiteSpace(name)) name = "project";
        string ysakPath = Path.Combine(folder, name + ProjectService.ProjectExtension);

        // An existing project (this name or a legacy project.ysak) is opened, not overwritten
        string? existing = File.Exists(ysakPath) ? ysakPath : ProjectService.ResolveProjectFileInFolder(folder);
        if (existing != null)
        {
            vm.OpenProject(existing);
        }
        else
        {
            vm.CreateProject(ysakPath);
        }
    }
}
