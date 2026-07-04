using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YoloHelperApp.Models;

namespace YoloHelperApp.ViewModels;

public partial class AnalyzeViewModel
{
    public async Task ScanRunsAsync(string projectFolder)
    {
        if (string.IsNullOrWhiteSpace(projectFolder) || !Directory.Exists(projectFolder)) return;

        IsLoading = true;
        HistoryLog = "Scanning training runs...";
        
        try
        {
            var newMetrics = new HashSet<string>();
            var scannedRuns = await Task.Run(() =>
            {
                var result = new List<TrainRun>();
                var targetDirs = new List<string>();
                var rootDirs = Directory.GetDirectories(projectFolder);

                foreach (var dir in rootDirs)
                {
                    targetDirs.Add(dir);
                    var name = Path.GetFileName(dir);
                    if (!name.Equals("ONNX", StringComparison.OrdinalIgnoreCase) && 
                        !name.Equals("images", StringComparison.OrdinalIgnoreCase) &&
                        !name.Equals("labels", StringComparison.OrdinalIgnoreCase))
                    {
                        targetDirs.AddRange(Directory.GetDirectories(dir, "*", SearchOption.AllDirectories));
                    }
                }

                foreach (var dir in targetDirs)
                {
                    if (Path.GetFileName(dir).Equals("weights", StringComparison.OrdinalIgnoreCase)) continue;

                    string weightsPath = Path.Combine(dir, "weights");
                    string resultsCsv = Path.Combine(dir, "results.csv");

                    int onnxCount = Services.YoloModelNamingHelper.CollectOnnxFiles(dir).Count;

                    if (Directory.Exists(weightsPath) || File.Exists(resultsCsv) || onnxCount > 0)
                    {
                        var run = new TrainRun
                        {
                            Name = Path.GetFileName(dir),
                            Path = dir,
                            Created = Directory.GetCreationTime(dir),
                            OnnxCount = onnxCount
                        };

                        if (File.Exists(resultsCsv))
                        {
                            var headers = ParseResultsCsv(run, resultsCsv);
                            foreach (var h in headers) newMetrics.Add(h);
                        }
                        
                        if (Directory.Exists(dir))
                        {
                            run.Images = Directory.GetFiles(dir, "*.jpg")
                                .Concat(Directory.GetFiles(dir, "*.png"))
                                .Take(12)
                                .Select(f => new Services.ThumbnailItem(f, decodeWidth: 240))
                                .ToList();
                        }

                        if (!result.Any(r => r.Path == run.Path))
                        {
                            result.Add(run);
                        }
                    }
                }
                return result;
            });

            string? prevSelectedPath = SelectedRun?.Path;

            var toRemove = Runs.Where(r => !scannedRuns.Any(sr => sr.Path == r.Path)).ToList();
            foreach (var r in toRemove) Runs.Remove(r);

            int colorIdx = 0;
            foreach (var sr in scannedRuns)
            {
                var existing = Runs.FirstOrDefault(r => r.Path == sr.Path);
                if (existing != null)
                {
                    existing.OnnxCount = sr.OnnxCount;
                    existing.Metrics = sr.Metrics;
                    existing.Images = sr.Images;
                    existing.LoadArgsInfo();
                }
                else
                {
                    sr.LoadMetadata();
                    if (sr.Color == "#3B82F6")
                    {
                        sr.Color = PaletteColors[colorIdx % PaletteColors.Length];
                    }
                    sr.Changed += () => ChartInvalidated?.Invoke();
                    sr.RunPostExportRequested += RunPostExport;
                    Runs.Add(sr);
                }
                colorIdx++;
            }

            bool addedAny = false;
            foreach (var metric in newMetrics)
            {
                if (!MetricToggles.Any(t => t.Name == metric))
                {
                    var extra = new MetricToggle { Name = metric, DisplayName = metric.Split('/').Last(), Group = "Other", Color = "#6B7280", IsEnabled = false };
                    extra.Changed += () => ChartInvalidated?.Invoke();
                    MetricToggles.Add(extra);
                    addedAny = true;
                }
            }

            if (addedAny)
            {
                NotifyGroupsChanged();
            }

            HistoryLog = $"Scanned: {Runs.Count} runs found.";
            
            if (!string.IsNullOrEmpty(prevSelectedPath))
            {
                var existing = Runs.FirstOrDefault(r => r.Path == prevSelectedPath);
                SelectedRun = existing ?? Runs.OrderByDescending(r => r.Created).FirstOrDefault();
            }
            else
            {
                SelectedRun = Runs.OrderByDescending(r => r.Created).FirstOrDefault();
            }

            if (SelectedRun != null)
            {
                SelectedRun.IsSelectedForChart = true;
            }
            ChartInvalidated?.Invoke();
        }
        catch (Exception ex)
        {
            HistoryLog = $"[ERROR] Scanning failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private List<string> ParseResultsCsv(TrainRun run, string filePath)
    {
        var foundHeaders = new List<string>();
        try
        {
            var lines = File.ReadAllLines(filePath);
            if (lines.Length < 2) return foundHeaders;

            var headers = lines[0].Split(',').Select(h => h.Trim()).ToList();
            var seriesList = headers.Skip(1).Select(h => new MetricSeries { Name = h }).ToList();

            foreach (var line in lines.Skip(1))
            {
                var parts = line.Split(',').Select(p => p.Trim()).ToList();
                if (parts.Count < headers.Count) continue;

                // Never read past the header count, and keep all series the same length
                // (an unparseable cell repeats the previous value instead of shifting the curve)
                for (int i = 1; i < headers.Count; i++)
                {
                    var series = seriesList[i - 1];
                    double val = double.TryParse(parts[i], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsed)
                        ? parsed
                        : (series.Values.Count > 0 ? series.Values[^1] : 0);
                    series.Values.Add(val);
                }
            }

            run.Metrics = seriesList;
            foreach (var s in seriesList) foundHeaders.Add(s.Name);
        }
        catch (Exception)
        {
            // Ignore errors
        }
        return foundHeaders;
    }
}
