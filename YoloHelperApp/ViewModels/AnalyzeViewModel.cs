using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YoloHelperApp.Models;
using YoloHelperApp.Services;
using System.Diagnostics;
using System.IO;

namespace YoloHelperApp.ViewModels;

public partial class AnalyzeViewModel : ViewModelBase
{
    private readonly YoloService _yoloService;
    private readonly InferenceService _inferenceService;
    private readonly ProjectService _projectService;

    public ExportViewModel ExportVM { get; }
    public InferencePreviewViewModel InferenceVM { get; }

    [ObservableProperty] private string _projectFolder = "";
    [ObservableProperty] private string _historyLog = "";
    [ObservableProperty] private bool _isLoading = false;

    public ObservableCollection<TrainRun> Runs { get; } = new();

    [ObservableProperty] private TrainRun? _selectedRun;

    public ICommand AutoExportOnnxCommand { get; }

    public AnalyzeViewModel(YoloService yoloService, InferenceService inferenceService, ProjectService projectService)
    {
        _yoloService = yoloService;
        _inferenceService = inferenceService;
        _projectService = projectService;

        ExportVM = new ExportViewModel(_yoloService, async () => {
            await ScanRunsAsync(ProjectFolder);
            InferenceVM?.ReloadModels();
        });
        InferenceVM = new InferencePreviewViewModel(_inferenceService, _yoloService, async () => {
            await ScanRunsAsync(ProjectFolder);
            InferenceVM?.ReloadModels();
        });

        AutoExportOnnxCommand = new AsyncRelayCommand<TrainRun>(AutoExportOnnxAsync);

        InitializeGraphToggles();
    }

    partial void OnProjectFolderChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        
        ExportVM.DatasetFolder = value;
        InferenceVM.ProjectFolder = value;
        
        _ = ScanRunsAsync(value);
        _ = InferenceVM.ScanImagesAsync();
    }

    partial void OnSelectedRunChanged(TrainRun? value)
    {
        ExportVM.SelectedRun = value;
        InferenceVM.SelectedRun = value;
    }

    public void RunPostExport(TrainRun run)
    {
        if (run == null) return;
        
        var settings = _projectService.LoadProject(ProjectFolder);
        if (settings.PostExports == null || settings.PostExports.Count == 0)
        {
            HistoryLog = "[INFO] No PostExport commands configured in project.ysak.";
            return;
        }

        // find onnx files (but ignore the Auto Inference ONNX ones unless we want to copy them too. We probably just want all onnx)
        var onnxFiles = new System.Collections.Generic.List<string>();
        string weightsPath = Path.Combine(run.Path, "weights");
        string exportedModelsPath = Path.Combine(run.Path, "ExportedModels");
        
        if (Directory.Exists(weightsPath)) {
            onnxFiles.AddRange(Directory.GetFiles(weightsPath, "*.onnx"));
        }
        if (Directory.Exists(exportedModelsPath)) {
            onnxFiles.AddRange(Directory.GetFiles(exportedModelsPath, "*.onnx"));
        }
        onnxFiles.AddRange(Directory.GetFiles(run.Path, "*.onnx"));
        
        // Exclude the auto inference one for post exports, usually users just want their actual exports for production
        var finalOnnxFiles = onnxFiles.Distinct()
            .Where(f => !Path.GetFileName(f).StartsWith("best_op12_fp32", StringComparison.OrdinalIgnoreCase))
            .ToList();
            
        string onnxCsv = string.Join(",", finalOnnxFiles);

        foreach (var cmd in settings.PostExports)
        {
            try
            {
                HistoryLog = $"[POST-EXPORT] Executing: {cmd}";
                // Basic implementation: if it ends with .ps1, run powershell
                if (cmd.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
                {
                    string scriptPath = Path.IsPathRooted(cmd) ? cmd : Path.Combine(ProjectFolder, cmd);
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -RunFolder \"{run.Path}\" -OnnxFiles \"{onnxCsv}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    Process.Start(psi);
                }
                else if (cmd.EndsWith(".bat", StringComparison.OrdinalIgnoreCase) || cmd.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase))
                {
                    string scriptPath = Path.IsPathRooted(cmd) ? cmd : Path.Combine(ProjectFolder, cmd);
                    var psi = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c \"{scriptPath}\" \"{run.Path}\" \"{onnxCsv}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    Process.Start(psi);
                }
                else
                {
                    // just run as generic command
                    var psi = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c {cmd} \"{run.Path}\" \"{onnxCsv}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    Process.Start(psi);
                }
            }
            catch (Exception ex)
            {
                HistoryLog = $"[POST-EXPORT ERROR] {ex.Message}";
            }
        }
    }

    private async Task AutoExportOnnxAsync(TrainRun? run)
    {
        if (run == null || !run.HasWeights) return;

        IsLoading = true;
        HistoryLog = $"Auto-exporting {run.Name} to ONNX...";
        
        try
        {
            var profile = new ExportProfile
            {
                Name = "Auto Inference ONNX",
                Format = "onnx",
                Oplets = new System.Collections.Generic.List<int> { 12 },
                BatchSizes = new System.Collections.Generic.List<string> { "1" },
                Precisions = new System.Collections.Generic.List<string> { "FP32" },
                ImgSizes = new System.Collections.Generic.List<int> { 640 },
                Simplify = true,
                InjectByteBGR = true
            };
            
            await _yoloService.RunExportAsync(ProjectFolder, run.BestModelPath, profile);
            
            // Re-scan to detect the new ONNX file
            await ScanRunsAsync(ProjectFolder);
            InferenceVM.ReloadModels();
            
            // Restore selection
            SelectedRun = Runs.FirstOrDefault(r => r.Path == run.Path);
            HistoryLog = $"Auto-export complete for {run.Name}";
        }
        catch (Exception ex)
        {
            HistoryLog = $"[ERROR] Auto-export failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
