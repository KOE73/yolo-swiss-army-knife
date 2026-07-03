using System;
using System.Linq;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using YoloHelperApp.ViewModels;
using YoloHelperApp.Services;

namespace YoloHelperApp.Views;

public partial class InferencePreviewView : UserControl
{
    public InferencePreviewView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
    }

    private void OnImageDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        var vm = DataContext as InferencePreviewViewModel;
        if (vm != null && !string.IsNullOrEmpty(vm.SelectedImagePath) && vm.ImageFiles.Count > 0)
        {
            var viewer = new ImageViewerWindow();
            viewer.LoadImages(vm.ImageFiles.ToList(), vm.SelectedImagePath);
            viewer.Show();
        }
    }

    private void OnImageListPointerWheelChanged(object? sender, Avalonia.Input.PointerWheelEventArgs e)
    {
        if (e.KeyModifiers == Avalonia.Input.KeyModifiers.Control)
        {
            var vm = DataContext as InferencePreviewViewModel;
            if (vm != null)
            {
                int newSize = vm.ThumbnailSize + (e.Delta.Y > 0 ? 20 : -20);
                if (newSize >= 50 && newSize <= 500)
                {
                    vm.ThumbnailSize = newSize;
                }
                e.Handled = true;
            }
        }
    }
}
