using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using YoloHelperApp.ViewModels;

namespace YoloHelperApp.Views;

public partial class ExportView : UserControl
{
    public ExportView()
    {
        InitializeComponent();
    }

    private async void OnBrowseBestPtClick(object sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select best.pt weights",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("PyTorch weights (*.pt)") { Patterns = new[] { "*.pt" } }
            }
        });

        if (files.Count > 0 && DataContext is ExportViewModel vm)
        {
            vm.BestPtPath = files[0].Path.LocalPath;
        }
    }

    private void ImgSizes_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ExportViewModel vm)
        {
            vm.NormalizeImgSizes();
        }
    }
}
