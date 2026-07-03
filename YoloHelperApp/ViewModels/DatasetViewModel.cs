using System;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YoloHelperApp.Services;

namespace YoloHelperApp.ViewModels;

public partial class ClassInfoItem : ObservableObject
{
    [ObservableProperty] private int _id;
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private int _annotationCount;
    [ObservableProperty] private double _percent;
}

public partial class DatasetViewModel : ViewModelBase
{
    private readonly DatasetService _datasetService;

    // ── Bound to MainWindowViewModel ───────────────────────────────────
    [ObservableProperty] private string _datasetFolder = "";

    // ── data.yaml info ─────────────────────────────────────────────────
    [ObservableProperty] private bool _hasDataYaml;
    [ObservableProperty] private string _trainPath = "";
    [ObservableProperty] private string _valPath = "";

    // ── Image / Label counts ───────────────────────────────────────────
    [ObservableProperty] private int _trainImages;
    [ObservableProperty] private int _valImages;
    [ObservableProperty] private int _totalImages;

    [ObservableProperty] private int _trainLabels;
    [ObservableProperty] private int _valLabels;
    [ObservableProperty] private int _totalLabels;

    // ── Matched / Unlabeled ────────────────────────────────────────────
    [ObservableProperty] private int _trainMatched;
    [ObservableProperty] private int _valMatched;
    [ObservableProperty] private int _totalMatched;
    [ObservableProperty] private int _trainUnlabeled;
    [ObservableProperty] private int _valUnlabeled;
    [ObservableProperty] private int _totalUnlabeled;

    // ── Percentages ────────────────────────────────────────────────────
    [ObservableProperty] private double _trainPercent;
    [ObservableProperty] private double _valPercent;

    // ── Annotations ────────────────────────────────────────────────────
    [ObservableProperty] private int _totalAnnotations;

    // ── Classes ────────────────────────────────────────────────────────
    public ObservableCollection<ClassInfoItem> Classes { get; } = new();

    // ── Reshuffle ratio ────────────────────────────────────────────────
    [ObservableProperty] private double _trainRatioPercent = 80;

    // ── UI state ───────────────────────────────────────────────────────
    [ObservableProperty] private string _statusLog = "";
    [ObservableProperty] private bool _isLoading;

    // Raised after successful reshuffle so other VMs can re-scan
    public event Action? ReshuffleCompleted;

    // ── Commands ───────────────────────────────────────────────────────
    public IAsyncRelayCommand ScanCommand { get; }
    public IAsyncRelayCommand ReshuffleCommand { get; }

    public DatasetViewModel(DatasetService datasetService)
    {
        _datasetService = datasetService;
        ScanCommand = new AsyncRelayCommand(UpdateStatsAsync);
        ReshuffleCommand = new AsyncRelayCommand(ReshuffleAsync);
    }

    // ── Scan ───────────────────────────────────────────────────────────

    public async Task UpdateStatsAsync()
    {
        if (string.IsNullOrWhiteSpace(DatasetFolder) || !Directory.Exists(DatasetFolder))
        {
            StatusLog = "Invalid dataset folder.";
            return;
        }

        IsLoading = true;
        StatusLog = "Scanning dataset...";
        try
        {
            var stats = await Task.Run(() => _datasetService.GetStats(DatasetFolder));

            // data.yaml
            HasDataYaml = stats.HasDataYaml;
            TrainPath = stats.YamlInfo?.TrainPath ?? "./images/train";
            ValPath = stats.YamlInfo?.ValPath ?? "./images/val";

            // Counts
            TrainImages = stats.TrainImages;
            ValImages = stats.ValImages;
            TotalImages = stats.TotalImages;

            TrainLabels = stats.TrainLabels;
            ValLabels = stats.ValLabels;
            TotalLabels = stats.TotalLabels;

            TrainMatched = stats.TrainMatched;
            ValMatched = stats.ValMatched;
            TotalMatched = stats.TotalMatched;
            TrainUnlabeled = stats.TrainUnlabeled;
            ValUnlabeled = stats.ValUnlabeled;
            TotalUnlabeled = stats.TotalUnlabeled;

            // Percentages
            TrainPercent = stats.TrainPercent;
            ValPercent = stats.ValPercent;

            // Annotations
            TotalAnnotations = stats.TotalAnnotations;

            // Classes
            Classes.Clear();
            var classNames = stats.YamlInfo?.Names ?? new();
            var sortedClassIds = stats.ClassAnnotationCounts.Keys
                .Union(classNames.Keys)
                .Distinct()
                .OrderBy(id => id)
                .ToList();

            foreach (var id in sortedClassIds)
            {
                stats.ClassAnnotationCounts.TryGetValue(id, out int count);
                classNames.TryGetValue(id, out string? name);
                Classes.Add(new ClassInfoItem
                {
                    Id = id,
                    Name = name ?? $"class_{id}",
                    AnnotationCount = count,
                    Percent = stats.TotalAnnotations > 0
                        ? Math.Round((double)count / stats.TotalAnnotations * 100, 1)
                        : 0
                });
            }

            StatusLog = $"Scanned: {TotalImages} images, {TotalLabels} labels, " +
                         $"{TotalAnnotations} annotations, {Classes.Count} classes.";
        }
        catch (Exception ex)
        {
            StatusLog = $"[ERROR] Scan failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ── Reshuffle ──────────────────────────────────────────────────────

    private async Task ReshuffleAsync()
    {
        if (string.IsNullOrWhiteSpace(DatasetFolder) || !Directory.Exists(DatasetFolder))
        {
            StatusLog = "[ERROR] Select directory first.";
            return;
        }

        IsLoading = true;
        StatusLog = $"Reshuffling to {TrainRatioPercent:F0}% / {100 - TrainRatioPercent:F0}%...";
        try
        {
            double ratio = TrainRatioPercent / 100.0;
            await Task.Run(() => _datasetService.Reshuffle(DatasetFolder, ratio));
            StatusLog = "Reshuffle completed (fast metadata moves, no SSD wear).";
            await UpdateStatsAsync();
            ReshuffleCompleted?.Invoke();
        }
        catch (Exception ex)
        {
            StatusLog = $"[ERROR] Reshuffle failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
