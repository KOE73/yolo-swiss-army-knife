using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using YoloHelperApp.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace YoloHelperApp.ViewModels;

// MetricToggle is identical to the one in HistoryViewModel
public partial class MetricToggle : ObservableObject
{
    [ObservableProperty] private bool _isEnabled;
    public string Name { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Group { get; init; } = "Other";
    public string Color { get; init; } = "#4B5563";
    public bool IsPrimary { get; init; } = false;

    public event Action? Changed;
    partial void OnIsEnabledChanged(bool value) => Changed?.Invoke();
}

public partial class AnalyzeViewModel
{
    private static readonly string[] PaletteColors = {
        "#EF4444", "#F97316", "#F59E0B", "#10B981", "#06B6D4", "#3B82F6",
        "#6366F1", "#8B5CF6", "#D946EF", "#EC4899", "#64748B", "#14B8A6"
    };

    private static readonly List<MetricToggle> DefaultToggles = new()
    {
        new MetricToggle { Name = "metrics/mAP50(B)",    DisplayName = "mAP50",    Group = "Performance", Color = "#2563EB", IsPrimary = true,  IsEnabled = true  },
        new MetricToggle { Name = "metrics/mAP50-95(B)", DisplayName = "mAP50-95", Group = "Performance", Color = "#1D4ED8", IsPrimary = true,  IsEnabled = true  },
        new MetricToggle { Name = "metrics/precision(B)", DisplayName = "precision",Group = "Performance", Color = "#0EA5E9", IsPrimary = false, IsEnabled = false },
        new MetricToggle { Name = "metrics/recall(B)",   DisplayName = "recall",   Group = "Performance", Color = "#38BDF8", IsPrimary = false, IsEnabled = false },

        new MetricToggle { Name = "val/box_loss",  DisplayName = "box", Group = "Val Loss", Color = "#F97316", IsPrimary = true,  IsEnabled = false },
        new MetricToggle { Name = "val/cls_loss",  DisplayName = "cls", Group = "Val Loss", Color = "#EF4444", IsPrimary = true,  IsEnabled = false },
        new MetricToggle { Name = "val/dfl_loss",  DisplayName = "dfl", Group = "Val Loss", Color = "#DC2626", IsPrimary = false, IsEnabled = false },

        new MetricToggle { Name = "train/box_loss", DisplayName = "box", Group = "Train Loss", Color = "#A78BFA", IsPrimary = false, IsEnabled = false },
        new MetricToggle { Name = "train/cls_loss", DisplayName = "cls", Group = "Train Loss", Color = "#C084FC", IsPrimary = false, IsEnabled = false },
        new MetricToggle { Name = "train/dfl_loss", DisplayName = "dfl", Group = "Train Loss", Color = "#D8B4FE", IsPrimary = false, IsEnabled = false },

        new MetricToggle { Name = "lr/pg0", DisplayName = "pg0", Group = "LR", Color = "#06B6D4", IsPrimary = false, IsEnabled = false },
        new MetricToggle { Name = "lr/pg1", DisplayName = "pg1", Group = "LR", Color = "#0891B2", IsPrimary = false, IsEnabled = false },
        new MetricToggle { Name = "lr/pg2", DisplayName = "pg2", Group = "LR", Color = "#0E7490", IsPrimary = false, IsEnabled = false },
    };

    public ObservableCollection<MetricToggle> MetricToggles { get; } = new();

    public IReadOnlyList<MetricToggle> PerformanceMetrics => MetricToggles.Where(t => t.Group == "Performance").ToList();
    public IReadOnlyList<MetricToggle> ValLossMetrics => MetricToggles.Where(t => t.Group == "Val Loss").ToList();
    public IReadOnlyList<MetricToggle> TrainLossMetrics => MetricToggles.Where(t => t.Group == "Train Loss").ToList();
    public IReadOnlyList<MetricToggle> LrMetrics => MetricToggles.Where(t => t.Group == "LR").ToList();
    public IEnumerable<MetricToggle> OtherMetrics => MetricToggles.Where(t => t.Group == "Other");

    public event Action? ChartInvalidated;

    private void InitializeGraphToggles()
    {
        foreach (var t in DefaultToggles)
        {
            t.Changed += () => ChartInvalidated?.Invoke();
            MetricToggles.Add(t);
        }
        NotifyGroupsChanged();
    }

    private void NotifyGroupsChanged()
    {
        OnPropertyChanged(nameof(PerformanceMetrics));
        OnPropertyChanged(nameof(ValLossMetrics));
        OnPropertyChanged(nameof(TrainLossMetrics));
        OnPropertyChanged(nameof(LrMetrics));
        OnPropertyChanged(nameof(OtherMetrics));
    }
}
