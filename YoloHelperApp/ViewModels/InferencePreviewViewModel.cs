using System;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YoloHelperApp.Services;
using YoloHelperApp.Models;

namespace YoloHelperApp.ViewModels;

public partial class InferencePreviewViewModel : ViewModelBase
{
    private readonly InferenceService _inferenceService;
    private readonly YoloService _yoloService;
    private readonly ProjectService _projectService;
    private readonly DatasetService _datasetService = new();
    private readonly Func<Task>? _onExportCompleted;

    private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".webp" };

    [ObservableProperty] private string _projectFolder = "";
    [ObservableProperty] private string _selectedModelPath = "";
    [ObservableProperty] private string _selectedImagePath = "";
    [ObservableProperty] private string _inferenceLog = "";
    [ObservableProperty] private string _currentTask = "detect";
    [ObservableProperty] private Avalonia.Media.Imaging.Bitmap? _renderedImage;
    [ObservableProperty] private int _thumbnailSize = 100;

    [ObservableProperty] private TrainRun? _selectedRun;

    // Assigned wholesale (not item-by-item) — folders can hold 10k+ images
    [ObservableProperty] private List<ThumbnailItem> _imageFiles = new();
    [ObservableProperty] private ThumbnailItem? _selectedImage;

    // Grid rows for the virtualized thumbnail grid: only visible rows materialize,
    // so thumbnails decode on demand while scrolling
    [ObservableProperty] private List<List<ThumbnailItem>> _imageRows = new();
    private int _columns = 2;
    private double _viewportWidth = 260;

    // Ultralytics-style confidence cut-off for the preview
    [ObservableProperty] private double _confThreshold = 0.25;

    public ObservableCollection<string> ModelFiles { get; } = new();
    public ObservableCollection<InferenceService.Prediction> Detections { get; } = new();

    [ObservableProperty] private bool _isLoading = false;

    public ICommand ScanImagesCommand { get; }
    public ICommand RunPredictCommand { get; }
    public ICommand AutoExportOnnxCommand { get; }
    public ICommand DeleteModelCommand { get; }

    public InferencePreviewViewModel(InferenceService inferenceService, YoloService yoloService, ProjectService projectService, Func<Task>? onExportCompleted = null)
    {
        _inferenceService = inferenceService;
        _yoloService = yoloService;
        _projectService = projectService;
        _onExportCompleted = onExportCompleted;

        ScanImagesCommand = new AsyncRelayCommand(ScanImagesAsync);
        RunPredictCommand = new AsyncRelayCommand(RunPredictAsync);
        AutoExportOnnxCommand = new AsyncRelayCommand(AutoExportOnnxAsync);
        DeleteModelCommand = new AsyncRelayCommand(DeleteSelectedModelAsync);
    }

    // ── Thumbnail grid layout ───────────────────────────────────────────

    /// <summary>Called from the view when the image panel is resized.</summary>
    public void SetViewportWidth(double width)
    {
        if (width <= 0) return;
        _viewportWidth = width;
        RecalculateColumns();
    }

    partial void OnThumbnailSizeChanged(int value) => RecalculateColumns();

    private void RecalculateColumns()
    {
        int columns = Math.Max(1, (int)(_viewportWidth / (ThumbnailSize + 10)));
        if (columns != _columns)
        {
            _columns = columns;
            RebuildRows();
        }
    }

    private void RebuildRows()
    {
        var rows = new List<List<ThumbnailItem>>();
        for (int i = 0; i < ImageFiles.Count; i += _columns)
        {
            rows.Add(ImageFiles.Skip(i).Take(_columns).ToList());
        }
        ImageRows = rows;
    }

    /// <summary>Selects a thumbnail tapped in the grid (rows are not selectable themselves).</summary>
    public void SelectImage(ThumbnailItem item)
    {
        if (SelectedImage != null) SelectedImage.IsSelected = false;
        item.IsSelected = true;
        SelectedImage = item;
    }

    partial void OnSelectedRunChanged(TrainRun? value)
    {
        ReloadModels();
    }

    partial void OnSelectedModelPathChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && File.Exists(value) && !string.IsNullOrWhiteSpace(SelectedImagePath) && File.Exists(SelectedImagePath))
        {
            _ = RunPredictAsync();
        }
    }

    public void ReloadModels()
    {
        ModelFiles.Clear();
        SelectedModelPath = "";
        
        if (SelectedRun != null)
        {
            foreach (var onnx in YoloModelNamingHelper.CollectOnnxFiles(SelectedRun.Path))
            {
                ModelFiles.Add(onnx);
            }

            // Try to select the auto-generated test ONNX first, otherwise select first available
            SelectedModelPath = ModelFiles.FirstOrDefault(YoloModelNamingHelper.IsAutoTestModel)
                                ?? ModelFiles.FirstOrDefault()
                                ?? "";
        }
    }

    partial void OnProjectFolderChanged(string value)
    {
        _ = ScanImagesAsync();
    }

    partial void OnSelectedImageChanged(ThumbnailItem? value)
    {
        SelectedImagePath = value?.Path ?? "";
    }

    partial void OnSelectedImagePathChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && File.Exists(value))
        {
            _ = RunPredictAsync();
        }
    }

    partial void OnConfThresholdChanged(double value)
    {
        // Re-render with the new cut-off if a prediction is already on screen
        if (!string.IsNullOrWhiteSpace(SelectedImagePath) && File.Exists(SelectedImagePath) &&
            !string.IsNullOrWhiteSpace(SelectedModelPath) && File.Exists(SelectedModelPath))
        {
            _ = RunPredictAsync();
        }
    }

    private async Task DeleteSelectedModelAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedModelPath) || !File.Exists(SelectedModelPath))
        {
            InferenceLog = "[ERROR] Select an ONNX model to delete first.";
            return;
        }

        string toDelete = SelectedModelPath;
        try
        {
            File.Delete(toDelete);
            InferenceLog = $"Deleted: {Path.GetFileName(toDelete)}";
            ReloadModels();
            // Refresh run list so ONNX counters update
            if (_onExportCompleted != null) await _onExportCompleted();
        }
        catch (Exception ex)
        {
            InferenceLog = $"[ERROR] Failed to delete {Path.GetFileName(toDelete)}: {ex.Message}";
        }
    }

    public async Task ScanImagesAsync()
    {
        if (string.IsNullOrWhiteSpace(ProjectFolder) || !Directory.Exists(ProjectFolder))
        {
            InferenceLog = "Invalid project directory.";
            return;
        }

        IsLoading = true;
        InferenceLog = "Scanning images...";

        try
        {
            var newImageFiles = await Task.Run(() =>
            {
                var imgList = new List<string>();

                static bool IsImage(string f) =>
                    ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant());

                // Scan images (train + val directories)
                string imgRoot = Path.Combine(ProjectFolder, "images");
                if (Directory.Exists(imgRoot))
                {
                    imgList.AddRange(Directory.GetFiles(imgRoot, "*.*", SearchOption.AllDirectories).Where(IsImage));
                }
                else
                {
                    // Fallback: scan root folder directly
                    imgList.AddRange(Directory.GetFiles(ProjectFolder, "*.*").Where(IsImage));
                }
                return imgList;
            });

            // Thumbnails decode lazily/async as items scroll into view (ThumbnailCache)
            ImageFiles = newImageFiles.Select(f => new ThumbnailItem(f, decodeWidth: 200)).ToList();
            RebuildRows();

            InferenceLog = $"Scanned: {ImageFiles.Count} images found.";
        }
        catch (Exception ex)
        {
            InferenceLog = $"[ERROR] Scanning failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task RunPredictAsync()
    {
        Detections.Clear();
        if (string.IsNullOrWhiteSpace(SelectedImagePath) || !File.Exists(SelectedImagePath))
        {
            InferenceLog = "[ERROR] Select an image first!";
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedModelPath) || !File.Exists(SelectedModelPath))
        {
            InferenceLog = "[ERROR] Select a model first!";
            return;
        }

        if (!SelectedModelPath.EndsWith(".onnx", StringComparison.OrdinalIgnoreCase))
        {
            InferenceLog = "[ERROR] Selected model must be in ONNX format. Convert it first using the Exporter!";
            return;
        }

        IsLoading = true;
        try
        {
            // The task comes from the run's args.yaml — the same source the model was trained with
            string task = SelectedRun?.Task ?? "detect";

            CurrentTask = task;
            InferenceLog = $"Model: {Path.GetFileName(SelectedModelPath)}\n";

            string imgPath = SelectedImagePath;
            string mdlPath = SelectedModelPath;
            string projFolder = ProjectFolder;
            float conf = (float)ConfThreshold;

            var results = await Task.Run(() =>
            {
                // Map class ids to readable names from data.yaml when present
                var classNames = _datasetService.TryParseDataYaml(projFolder)?.Names;
                return _inferenceService.RunInference(imgPath, mdlPath, task, classNames, conf);
            });

            foreach (var d in results.Predictions) Detections.Add(d);

            if (results.RenderedImage != null)
            {
                using var ms = new MemoryStream(results.RenderedImage);
                RenderedImage = new Avalonia.Media.Imaging.Bitmap(ms);
            }
            else
            {
                RenderedImage = null;
            }

            // Explicit pipeline trace so task/model mismatches are visible at a glance
            InferenceLog += $"Task '{task}' (from args.yaml) -> {results.FactoryUsed}\n";
            InferenceLog += $"Input {results.InputShape}, output {results.OutputShape}, backend {results.Backend}\n";
            if (!string.IsNullOrEmpty(results.Warning))
                InferenceLog += $"[WARNING] {results.Warning}\n";
            InferenceLog += $"Detections: {results.Kept} of {results.TotalRaw} raw (conf >= {ConfThreshold:0.00}).";
        }
        catch (Exception ex)
        {
            InferenceLog += $"[ERROR] Inference failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task AutoExportOnnxAsync()
    {
        if (SelectedRun == null || !SelectedRun.HasWeights)
        {
            InferenceLog = "[ERROR] Select a training run with valid weights first!";
            return;
        }

        IsLoading = true;
        InferenceLog = $"Generating Test ONNX model for {SelectedRun.Name}...";
        try
        {
            // Match the training resolution and task (imgsz from args.yaml; the task is
            // baked into best.pt, so an obb run exports an obb ONNX automatically)
            var profile = YoloModelNamingHelper.CreateAutoTestProfile(
                SelectedRun.Task, SelectedRun.ImgSize, injectByteBgr: true, _projectService.Current.Tools.OnnxToolsPath);

            await _yoloService.RunExportAsync(ProjectFolder, SelectedRun.BestModelPath, profile);

            // Re-scan and select the newly created model
            ReloadModels();

            InferenceLog = $"Test ONNX generated successfully. Model: {Path.GetFileName(SelectedModelPath)}";
        }
        catch (Exception ex)
        {
            InferenceLog = $"[ERROR] Test ONNX generation failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            if (_onExportCompleted != null)
            {
                await _onExportCompleted();
            }
        }
    }
}
