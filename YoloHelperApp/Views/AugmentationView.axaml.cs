using Avalonia.Controls;

namespace YoloHelperApp.Views;

public partial class AugmentationView : UserControl
{
    public AugmentationView()
    {
        InitializeComponent();
    }

    private async void OnExportAllClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
        {
            Title = "Select Export Folder for YOLO Augmentations",
            AllowMultiple = false
        });

        if (folders.Count > 0 && DataContext is YoloHelperApp.ViewModels.AugmentationViewModel vm)
        {
            vm.ExportAllProfiles(folders[0].Path.LocalPath);
            
            // Optional: show a quick message or notification, but for now we just export silently.
        }
    }
}
