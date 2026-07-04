using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YoloHelperApp.Services;

namespace YoloHelperApp.Models;

public class MetricSeries
{
    public string Name { get; set; } = "";
    public List<double> Values { get; set; } = new();
}

public partial class TrainRun : ObservableObject
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _path = "";
    [ObservableProperty] private string _task = "detect";
    [ObservableProperty] private DateTime _created;
    [ObservableProperty] private List<MetricSeries> _metrics = new();
    [ObservableProperty] private List<ThumbnailItem> _images = new();

    [ObservableProperty] private int _onnxCount;
    public bool HasOnnx => OnnxCount > 0;

    // Parsed from args.yaml (training parameters)
    [ObservableProperty] private int _imgSize = 640;
    [ObservableProperty] private string _baseModel = "";
    [ObservableProperty] private int _epochs;
    [ObservableProperty] private string _batch = "";
    [ObservableProperty] private bool _hasArgsInfo;

    /// <summary>One-line summary of the training setup shown in the runs list.</summary>
    public string Info
    {
        get
        {
            if (!HasArgsInfo) return "";
            var parts = new List<string> { Task };
            if (!string.IsNullOrEmpty(BaseModel)) parts.Add(BaseModel);
            parts.Add($"imgsz {ImgSize}");
            if (Epochs > 0) parts.Add($"{Epochs} ep");
            if (!string.IsNullOrEmpty(Batch)) parts.Add($"batch {Batch}");
            return string.Join(" · ", parts);
        }
    }

    public string BestModelPath => System.IO.Path.Combine(Path, "weights", "best.pt");
    public bool HasWeights => File.Exists(BestModelPath);

    [ObservableProperty] private bool _isSelectedForChart;
    [ObservableProperty] private string _color = "#3B82F6"; // Default color

    public ICommand SetColorCommand { get; }
    public ICommand RunPostExportScriptCommand { get; }

    public event Action? Changed;
    public event Action<TrainRun>? RunPostExportRequested;

    public TrainRun()
    {
        SetColorCommand = new RelayCommand<string>(color =>
        {
            if (!string.IsNullOrEmpty(color))
            {
                Color = color;
                SaveMetadata();
                Changed?.Invoke();
            }
        });

        RunPostExportScriptCommand = new RelayCommand(() =>
        {
            RunPostExportRequested?.Invoke(this);
        });
    }

    partial void OnIsSelectedForChartChanged(bool value) => Changed?.Invoke();

    partial void OnColorChanged(string value)
    {
        SaveMetadata();
        Changed?.Invoke();
    }

    partial void OnOnnxCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasOnnx));
    }

    public void LoadMetadata()
    {
        string metaPath = System.IO.Path.Combine(Path, "ysak_meta.json");
        if (File.Exists(metaPath))
        {
            try
            {
                string content = File.ReadAllText(metaPath);
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(content);
                if (dict != null && dict.TryGetValue("color", out string? savedColor))
                {
                    Color = savedColor;
                }
            }
            catch { }
        }

        LoadArgsInfo();
    }

    /// <summary>Parses the ultralytics args.yaml written into every run folder.</summary>
    public void LoadArgsInfo()
    {
        string argsPath = System.IO.Path.Combine(Path, "args.yaml");
        if (!File.Exists(argsPath)) return;

        try
        {
            var args = ParseSimpleYaml(File.ReadAllLines(argsPath));
            HasArgsInfo = args.Count > 0;

            if (args.TryGetValue("task", out var task) && !string.IsNullOrEmpty(task))
                Task = task;
            if (args.TryGetValue("model", out var model) && !string.IsNullOrEmpty(model))
                BaseModel = System.IO.Path.GetFileName(model);
            if (args.TryGetValue("imgsz", out var imgsz) && int.TryParse(imgsz, out int size) && size > 0)
                ImgSize = size;
            if (args.TryGetValue("epochs", out var epochs) && int.TryParse(epochs, out int ep))
                Epochs = ep;
            if (args.TryGetValue("batch", out var batch) && !string.IsNullOrEmpty(batch))
                Batch = batch;

            OnPropertyChanged(nameof(Info));
        }
        catch { }
    }

    /// <summary>Flat "key: value" YAML parsing — enough for ultralytics args.yaml top-level scalars.</summary>
    private static Dictionary<string, string> ParseSimpleYaml(IEnumerable<string> lines)
    {
        var result = new Dictionary<string, string>();
        foreach (var line in lines)
        {
            if (line.Length == 0 || line[0] == ' ' || line[0] == '#' || line[0] == '-') continue;
            var parts = line.Split(':', 2);
            if (parts.Length != 2) continue;

            string key = parts[0].Trim();
            string value = parts[1].Trim().Trim('\'', '"');
            if (key.Length > 0 && value != "null")
                result[key] = value;
        }
        return result;
    }

    private void SaveMetadata()
    {
        string metaPath = System.IO.Path.Combine(Path, "ysak_meta.json");
        try
        {
            var dict = new Dictionary<string, string> { { "color", Color } };
            string content = JsonSerializer.Serialize(dict);
            File.WriteAllText(metaPath, content);
        }
        catch { }
    }
}
