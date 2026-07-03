using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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
    [ObservableProperty] private List<string> _images = new();
    
    [ObservableProperty] private int _onnxCount;
    public bool HasOnnx => OnnxCount > 0;

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

        string argsPath = System.IO.Path.Combine(Path, "args.yaml");
        if (File.Exists(argsPath))
        {
            try
            {
                var lines = File.ReadAllLines(argsPath);
                foreach (var line in lines)
                {
                    if (line.StartsWith("task:"))
                    {
                        var parts = line.Split(':', 2);
                        if (parts.Length == 2)
                        {
                            Task = parts[1].Trim().Trim('\'', '"');
                        }
                        break;
                    }
                }
            }
            catch { }
        }
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
