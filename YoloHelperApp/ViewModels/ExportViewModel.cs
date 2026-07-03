using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YoloHelperApp.Models;
using YoloHelperApp.Services;

namespace YoloHelperApp.ViewModels;

public partial class ExportViewModel : ViewModelBase
{
    private readonly YoloService _yoloService;
    private readonly Func<Task>? _onExportCompleted;

    [ObservableProperty] private string _datasetFolder = "";
    
    [ObservableProperty] private TrainRun? _selectedRun;
    partial void OnSelectedRunChanged(TrainRun? value)
    {
        BestPtPath = value?.BestModelPath ?? "";
    }
    
    [ObservableProperty] private string _bestPtPath = "";
    [ObservableProperty] private string _exportLog = "";
    [ObservableProperty] private bool _isExporting = false;

    // Profiles List
    public ObservableCollection<ExportProfile> Profiles { get; } = new();
    [ObservableProperty] private ExportProfile? _selectedProfile;

    // Profile properties bindings
    [ObservableProperty] private string _profileName = "";
    [ObservableProperty] private string _format = "onnx";
    [ObservableProperty] private string _opsetVersionCsv = "12, 13, 18";
    [ObservableProperty] private string _batchSizesCsv = "1, 4, dynamic";
    [ObservableProperty] private string _exportImgSizesCsv = "640";
    [ObservableProperty] private bool _exportFp32 = true;
    [ObservableProperty] private bool _exportFp16 = false;
    [ObservableProperty] private bool _exportInt8 = false;
    [ObservableProperty] private bool _simplify = true;
    [ObservableProperty] private bool _nms = false;
    [ObservableProperty] private bool _injectByteBgr = false;
    
    [ObservableProperty] private bool _isByteBgrAvailable = false;

    public ObservableCollection<string> AvailableFormats { get; } = new()
    {
        "onnx", "torchscript", "openvino", "engine", "coreml", "saved_model", "pb", "tflite", "edgetpu", "tfjs", "paddle", "ncnn"
    };

    public ICommand AddProfileCommand { get; }
    public ICommand DeleteProfileCommand { get; }
    public ICommand StartBatchExportCommand { get; }
    public ICommand ExportAllProfilesCommand { get; }

    public ExportViewModel(YoloService yoloService, Func<Task>? onExportCompleted = null)
    {
        _yoloService = yoloService;
        _onExportCompleted = onExportCompleted;
        _yoloService.OnLogReceived += log => ExportLog += log + "\n";

        AddProfileCommand = new RelayCommand(AddProfile);
        DeleteProfileCommand = new RelayCommand(DeleteProfile);
        StartBatchExportCommand = new AsyncRelayCommand(StartBatchExportAsync);
        ExportAllProfilesCommand = new AsyncRelayCommand(ExportAllProfilesAsync);

        IsByteBgrAvailable = CheckByteBgrAvailable();

        // Load some defaults
        var defaultProfile = new ExportProfile
        {
            Name = "Default ONNX",
            Format = "onnx",
            Oplets = new List<int> { 12, 13, 18 },
            BatchSizes = new List<string> { "1", "4", "dynamic" },
            Precisions = new List<string> { "FP32" },
            InjectByteBGR = IsByteBgrAvailable
        };
        Profiles.Add(defaultProfile);
        SelectedProfile = defaultProfile;
        LoadProfileToEditor(defaultProfile);
    }
    
    private bool CheckByteBgrAvailable()
    {
        var envPath = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(envPath)) return false;
        
        var paths = envPath.Split(Path.PathSeparator);
        foreach (var path in paths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    if (File.Exists(Path.Combine(path, "NeuroModFlowNet.ONNX.Tools.exe")) || 
                        File.Exists(Path.Combine(path, "NeuroModFlowNet.ONNX.Tools")))
                    {
                        return true;
                    }
                }
            }
            catch { /* Ignore access errors */ }
        }
        return false;
    }

    partial void OnSelectedProfileChanged(ExportProfile? oldValue, ExportProfile? newValue)
    {
        if (newValue != null) LoadProfileToEditor(newValue);
    }

    private void LoadProfileToEditor(ExportProfile p)
    {
        ProfileName = p.Name;
        Format = p.Format;
        OpsetVersionCsv = string.Join(", ", p.Oplets);
        BatchSizesCsv = string.Join(", ", p.BatchSizes);
        ExportImgSizesCsv = string.Join(", ", p.ImgSizes);
        ExportFp32 = p.Precisions.Contains("FP32", StringComparer.OrdinalIgnoreCase);
        ExportFp16 = p.Precisions.Contains("FP16", StringComparer.OrdinalIgnoreCase);
        ExportInt8 = p.Precisions.Contains("INT8", StringComparer.OrdinalIgnoreCase);
        Simplify = p.Simplify;
        Nms = p.Nms;
        InjectByteBgr = p.InjectByteBGR;
    }

    public void SaveCurrentProfileEditor()
    {
        if (SelectedProfile == null) return;
        SelectedProfile.Name = ProfileName;
        SelectedProfile.Format = Format;
        SelectedProfile.Oplets = OpsetVersionCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out int v) ? v : 18).ToList();
        SelectedProfile.BatchSizes = BatchSizesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        
        SelectedProfile.ImgSizes = ExportImgSizesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out int v) ? v : 640)
            .Select(v => Math.Max(32, (int)Math.Round(v / 32.0) * 32))
            .Distinct()
            .ToList();
        if (SelectedProfile.ImgSizes.Count == 0) SelectedProfile.ImgSizes.Add(640);
        
        var precisions = new List<string>();
        if (ExportFp32) precisions.Add("FP32");
        if (ExportFp16) precisions.Add("FP16");
        if (ExportInt8) precisions.Add("INT8");
        if (precisions.Count == 0) precisions.Add("FP32"); // Fallback
        
        SelectedProfile.Precisions = precisions;
        SelectedProfile.Simplify = Simplify;
        SelectedProfile.Nms = Nms;
        SelectedProfile.InjectByteBGR = InjectByteBgr;

        // Force UI refresh on list item
        int index = Profiles.IndexOf(SelectedProfile);
        if (index >= 0)
        {
            Profiles[index] = SelectedProfile;
            SelectedProfile = Profiles[index];
        }
    }

    public void NormalizeImgSizes()
    {
        var parsed = ExportImgSizesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out int v) ? v : 640)
            .Select(v => Math.Max(32, (int)Math.Round(v / 32.0) * 32))
            .Distinct()
            .ToList();
            
        if (parsed.Count == 0) parsed.Add(640);
        ExportImgSizesCsv = string.Join(", ", parsed);
        SaveCurrentProfileEditor();
    }

    private void AddProfile()
    {
        var p = new ExportProfile { Name = $"New Profile {Profiles.Count + 1}" };
        Profiles.Add(p);
        SelectedProfile = p;
    }

    private void DeleteProfile()
    {
        if (SelectedProfile == null) return;
        Profiles.Remove(SelectedProfile);
        SelectedProfile = Profiles.FirstOrDefault();
    }

    private async Task StartBatchExportAsync()
    {
        if (SelectedProfile == null) return;
        SaveCurrentProfileEditor();
        await ExecuteProfilesExportAsync(new List<ExportProfile> { SelectedProfile });
    }
    
    private async Task ExportAllProfilesAsync()
    {
        SaveCurrentProfileEditor();
        var activeProfiles = Profiles.Where(p => p.IsActive).ToList();
        await ExecuteProfilesExportAsync(activeProfiles);
    }

    private async Task ExecuteProfilesExportAsync(List<ExportProfile> profilesToExport)
    {
        if (string.IsNullOrWhiteSpace(BestPtPath) || !File.Exists(BestPtPath))
        {
            ExportLog += "[ERROR] Select a valid best.pt weights file first!\n";
            return;
        }

        if (!profilesToExport.Any())
        {
            ExportLog += "[WARNING] No profiles selected for export.\n";
            return;
        }

        IsExporting = true;
        try
        {
            foreach (var profile in profilesToExport)
            {
                ExportLog += $"Running batch export for profile: {profile.Name}...\n";
                await _yoloService.RunExportAsync(DatasetFolder, BestPtPath, profile);
            }
            ExportLog += "All batch exports completed.\n";
        }
        catch (Exception ex)
        {
            ExportLog += $"[ERROR] Export failed: {ex.Message}\n";
        }
        finally
        {
            IsExporting = false;
            if (_onExportCompleted != null)
            {
                await _onExportCompleted();
            }
        }
    }
}
