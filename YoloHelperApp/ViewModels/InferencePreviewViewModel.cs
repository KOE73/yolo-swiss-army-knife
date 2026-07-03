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
    private readonly Func<Task>? _onExportCompleted;

    [ObservableProperty] private string _projectFolder = "";
    [ObservableProperty] private string _selectedModelPath = "";
    [ObservableProperty] private string _selectedImagePath = "";
    [ObservableProperty] private string _inferenceLog = "";
    [ObservableProperty] private string _currentTask = "detect";
    [ObservableProperty] private Avalonia.Media.Imaging.Bitmap? _renderedImage;
    [ObservableProperty] private int _thumbnailSize = 100;

    [ObservableProperty] private TrainRun? _selectedRun;

    public ObservableCollection<string> ImageFiles { get; } = new();
    public ObservableCollection<string> ModelFiles { get; } = new();
    public ObservableCollection<InferenceService.Prediction> Detections { get; } = new();

    [ObservableProperty] private bool _isLoading = false;

    public ICommand ScanImagesCommand { get; }
    public ICommand RunPredictCommand { get; }
    public ICommand AutoExportOnnxCommand { get; }

    public InferencePreviewViewModel(InferenceService inferenceService, YoloService yoloService, Func<Task>? onExportCompleted = null)
    {
        _inferenceService = inferenceService;
        _yoloService = yoloService;
        _onExportCompleted = onExportCompleted;

        ScanImagesCommand = new AsyncRelayCommand(ScanImagesAsync);
        RunPredictCommand = new AsyncRelayCommand(RunPredictAsync);
        AutoExportOnnxCommand = new AsyncRelayCommand(AutoExportOnnxAsync);
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
            var onnxFiles = new List<string>();
            string weightsPath = Path.Combine(SelectedRun.Path, "weights");
            
            if (Directory.Exists(weightsPath))
            {
                onnxFiles.AddRange(Directory.GetFiles(weightsPath, "*.onnx"));
            }
            onnxFiles.AddRange(Directory.GetFiles(SelectedRun.Path, "*.onnx"));
            
            foreach (var onnx in onnxFiles.Distinct())
            {
                ModelFiles.Add(onnx);
            }
            
            // Try to select the test ONNX first, otherwise select first available
            SelectedModelPath = ModelFiles.FirstOrDefault(f => Path.GetFileName(f).StartsWith("best_img640_op12_fp32", StringComparison.OrdinalIgnoreCase)) 
                                ?? ModelFiles.FirstOrDefault() 
                                ?? "";
        }
    }

    partial void OnProjectFolderChanged(string value)
    {
        _ = ScanImagesAsync();
    }

    partial void OnSelectedImagePathChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && File.Exists(value))
        {
            _ = RunPredictAsync();
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

                // Scan images (train + val directories)
                string imgRoot = Path.Combine(ProjectFolder, "images");
                if (Directory.Exists(imgRoot))
                {
                    var files = Directory.GetFiles(imgRoot, "*.*", SearchOption.AllDirectories)
                        .Where(f => f.EndsWith(".jpg") || f.EndsWith(".jpeg") || f.EndsWith(".png"))
                        .ToList();
                    imgList.AddRange(files);
                }
                else
                {
                    // Fallback: scan root folder directly
                    var files = Directory.GetFiles(ProjectFolder, "*.*")
                        .Where(f => f.EndsWith(".jpg") || f.EndsWith(".jpeg") || f.EndsWith(".png"))
                        .ToList();
                    imgList.AddRange(files);
                }
                return imgList;
            });

            ImageFiles.Clear();
            foreach (var f in newImageFiles) ImageFiles.Add(f);

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
            // Auto detect task if not explicit
            string task = SelectedRun?.Task ?? "detect";

            CurrentTask = task;
            InferenceLog = $"Loading ONNX model: {Path.GetFileName(SelectedModelPath)} for task {task}...\n";

            string imgPath = SelectedImagePath;
            string mdlPath = SelectedModelPath;
            
            var results = await Task.Run(() => _inferenceService.RunInference(imgPath, mdlPath, task));
            
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
            
            InferenceLog += $"Inference completed: {Detections.Count} objects detected.";
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
            var profile = new ExportProfile
            {
                Name = "Auto Inference ONNX",
                Format = "onnx",
                Oplets = new List<int> { 12 },
                BatchSizes = new List<string> { "1" },
                Precisions = new List<string> { "FP32" },
                ImgSizes = new List<int> { 640 },
                Simplify = true,
                InjectByteBGR = true
            };

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
