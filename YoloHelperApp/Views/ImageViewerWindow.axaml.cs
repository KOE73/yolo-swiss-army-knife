using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Interactivity;
using System.IO;

namespace YoloHelperApp.Views;

public partial class ImageViewerWindow : Window
{
    private List<string> _images = new();
    private int _currentIndex = 0;

    public ImageViewerWindow()
    {
        InitializeComponent();
    }

    public void LoadImages(List<string> images, string initialImage)
    {
        _images = images ?? new List<string>();
        if (_images.Count == 0) return;

        _currentIndex = _images.IndexOf(initialImage);
        if (_currentIndex < 0) _currentIndex = 0;

        UpdateImage();
    }

    private void UpdateImage()
    {
        if (_images.Count == 0 || _currentIndex < 0 || _currentIndex >= _images.Count)
            return;

        try
        {
            var path = _images[_currentIndex];
            if (File.Exists(path))
            {
                MainImage.Source = new Bitmap(path);
                IndexText.Text = $"{_currentIndex + 1} / {_images.Count} - {Path.GetFileName(path)}";
            }
        }
        catch (Exception ex)
        {
            IndexText.Text = $"Error loading image: {ex.Message}";
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Right || e.Key == Key.Down)
        {
            _currentIndex = (_currentIndex + 1) % _images.Count;
            UpdateImage();
        }
        else if (e.Key == Key.Left || e.Key == Key.Up)
        {
            _currentIndex = (_currentIndex - 1 + _images.Count) % _images.Count;
            UpdateImage();
        }
        else if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (e.Delta.Y < 0)
        {
            // Scroll down -> next image
            _currentIndex = (_currentIndex + 1) % _images.Count;
            UpdateImage();
        }
        else if (e.Delta.Y > 0)
        {
            // Scroll up -> previous image
            _currentIndex = (_currentIndex - 1 + _images.Count) % _images.Count;
            UpdateImage();
        }
    }
}
