using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace YoloHelperApp.Services;

public class DatasetService
{
    private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".webp" };

    // ── data.yaml parsing ──────────────────────────────────────────────

    public class DataYamlInfo
    {
        public Dictionary<int, string> Names { get; set; } = new();
        public string TrainPath { get; set; } = "";
        public string ValPath { get; set; } = "";
        public string BasePath { get; set; } = ".";
    }

    public DataYamlInfo? TryParseDataYaml(string projectFolder)
    {
        string yamlPath = Path.Combine(projectFolder, "data.yaml");
        if (!File.Exists(yamlPath)) return null;

        try
        {
            var text = File.ReadAllText(yamlPath);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var raw = deserializer.Deserialize<Dictionary<string, object>>(text);
            var info = new DataYamlInfo();

            // Parse names
            if (raw.TryGetValue("names", out var namesObj))
            {
                if (namesObj is Dictionary<object, object> namesDict)
                {
                    foreach (var kvp in namesDict)
                    {
                        if (int.TryParse(kvp.Key?.ToString(), out int id))
                            info.Names[id] = kvp.Value?.ToString() ?? $"class_{id}";
                    }
                }
                else if (namesObj is List<object> namesList)
                {
                    for (int i = 0; i < namesList.Count; i++)
                        info.Names[i] = namesList[i]?.ToString() ?? $"class_{i}";
                }
            }

            if (raw.TryGetValue("path", out var pathObj))
                info.BasePath = pathObj?.ToString() ?? ".";
            if (raw.TryGetValue("train", out var trainObj))
                info.TrainPath = trainObj?.ToString() ?? "";
            if (raw.TryGetValue("val", out var valObj))
                info.ValPath = valObj?.ToString() ?? "";

            return info;
        }
        catch
        {
            return null;
        }
    }

    // ── Stats ──────────────────────────────────────────────────────────

    public class DatasetStats
    {
        // Images
        public int TrainImages { get; set; }
        public int ValImages { get; set; }
        public int TotalImages => TrainImages + ValImages;

        // Labels
        public int TrainLabels { get; set; }
        public int ValLabels { get; set; }
        public int TotalLabels => TrainLabels + ValLabels;

        // Matched (image has corresponding label)
        public int TrainMatched { get; set; }
        public int ValMatched { get; set; }
        public int TrainUnlabeled => TrainImages - TrainMatched;
        public int ValUnlabeled => ValImages - ValMatched;
        public int TotalMatched => TrainMatched + ValMatched;
        public int TotalUnlabeled => TotalImages - TotalMatched;

        // Percentages
        public double TrainPercent => TotalImages > 0 ? Math.Round((double)TrainImages / TotalImages * 100, 1) : 0;
        public double ValPercent => TotalImages > 0 ? Math.Round((double)ValImages / TotalImages * 100, 1) : 0;

        // Per-class annotation counts (class_id -> count)
        public Dictionary<int, int> ClassAnnotationCounts { get; set; } = new();
        public int TotalAnnotations => ClassAnnotationCounts.Values.Sum();

        // data.yaml
        public DataYamlInfo? YamlInfo { get; set; }
        public bool HasDataYaml => YamlInfo != null;
    }

    public DatasetStats GetStats(string projectFolder)
    {
        var stats = new DatasetStats();
        if (string.IsNullOrWhiteSpace(projectFolder) || !Directory.Exists(projectFolder))
            return stats;

        // Parse data.yaml
        stats.YamlInfo = TryParseDataYaml(projectFolder);

        // Resolve actual paths from data.yaml or fall back to convention
        string trainImgDir, valImgDir, trainLblDir, valLblDir;

        if (stats.YamlInfo != null)
        {
            string basePath = stats.YamlInfo.BasePath == "."
                ? projectFolder
                : Path.GetFullPath(Path.Combine(projectFolder, stats.YamlInfo.BasePath));

            trainImgDir = Path.GetFullPath(Path.Combine(basePath, stats.YamlInfo.TrainPath));
            valImgDir = Path.GetFullPath(Path.Combine(basePath, stats.YamlInfo.ValPath));

            // Labels mirror images: images/train -> labels/train
            trainLblDir = trainImgDir.Replace(
                Path.DirectorySeparatorChar + "images" + Path.DirectorySeparatorChar,
                Path.DirectorySeparatorChar + "labels" + Path.DirectorySeparatorChar);
            valLblDir = valImgDir.Replace(
                Path.DirectorySeparatorChar + "images" + Path.DirectorySeparatorChar,
                Path.DirectorySeparatorChar + "labels" + Path.DirectorySeparatorChar);
        }
        else
        {
            // Standard YOLO convention
            trainImgDir = Path.Combine(projectFolder, "images", "train");
            valImgDir = Path.Combine(projectFolder, "images", "val");
            trainLblDir = Path.Combine(projectFolder, "labels", "train");
            valLblDir = Path.Combine(projectFolder, "labels", "val");
        }

        // Count images
        stats.TrainImages = CountImages(trainImgDir);
        stats.ValImages = CountImages(valImgDir);

        // Count labels
        stats.TrainLabels = CountLabels(trainLblDir);
        stats.ValLabels = CountLabels(valLblDir);

        // Count matched pairs and per-class annotations
        stats.TrainMatched = CountMatchedAndAnnotations(trainImgDir, trainLblDir, stats.ClassAnnotationCounts);
        stats.ValMatched = CountMatchedAndAnnotations(valImgDir, valLblDir, stats.ClassAnnotationCounts);

        return stats;
    }

    // ── Reshuffle ──────────────────────────────────────────────────────

    public void Reshuffle(string projectFolder, double trainRatio)
    {
        if (string.IsNullOrWhiteSpace(projectFolder) || !Directory.Exists(projectFolder))
            return;

        var yamlInfo = TryParseDataYaml(projectFolder);

        string trainImgDir, valImgDir, trainLblDir, valLblDir;

        if (yamlInfo != null)
        {
            string basePath = yamlInfo.BasePath == "."
                ? projectFolder
                : Path.GetFullPath(Path.Combine(projectFolder, yamlInfo.BasePath));

            trainImgDir = Path.GetFullPath(Path.Combine(basePath, yamlInfo.TrainPath));
            valImgDir = Path.GetFullPath(Path.Combine(basePath, yamlInfo.ValPath));
            trainLblDir = trainImgDir.Replace(
                Path.DirectorySeparatorChar + "images" + Path.DirectorySeparatorChar,
                Path.DirectorySeparatorChar + "labels" + Path.DirectorySeparatorChar);
            valLblDir = valImgDir.Replace(
                Path.DirectorySeparatorChar + "images" + Path.DirectorySeparatorChar,
                Path.DirectorySeparatorChar + "labels" + Path.DirectorySeparatorChar);
        }
        else
        {
            trainImgDir = Path.Combine(projectFolder, "images", "train");
            valImgDir = Path.Combine(projectFolder, "images", "val");
            trainLblDir = Path.Combine(projectFolder, "labels", "train");
            valLblDir = Path.Combine(projectFolder, "labels", "val");
        }

        // Ensure directories exist
        Directory.CreateDirectory(trainImgDir);
        Directory.CreateDirectory(valImgDir);
        Directory.CreateDirectory(trainLblDir);
        Directory.CreateDirectory(valLblDir);

        // Collect all image-label pairs from both train and val into a single pool
        var allPairs = new List<(string ImagePath, string? LabelPath)>();

        CollectPairs(trainImgDir, trainLblDir, allPairs);
        CollectPairs(valImgDir, valLblDir, allPairs);

        // Deduplicate by filename
        allPairs = allPairs
            .GroupBy(p => Path.GetFileNameWithoutExtension(p.ImagePath))
            .Select(g => g.First())
            .ToList();

        // Shuffle
        var rng = new Random();
        var shuffled = allPairs.OrderBy(_ => rng.Next()).ToList();

        int trainCount = (int)Math.Round(shuffled.Count * trainRatio);

        // Move files
        for (int i = 0; i < shuffled.Count; i++)
        {
            var pair = shuffled[i];
            bool isTrain = i < trainCount;

            string targetImgDir = isTrain ? trainImgDir : valImgDir;
            string targetLblDir = isTrain ? trainLblDir : valLblDir;

            SafeMove(pair.ImagePath, Path.Combine(targetImgDir, Path.GetFileName(pair.ImagePath)));
            if (pair.LabelPath != null)
                SafeMove(pair.LabelPath, Path.Combine(targetLblDir, Path.GetFileName(pair.LabelPath)));
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private int CountImages(string dir)
    {
        if (!Directory.Exists(dir)) return 0;
        return Directory.GetFiles(dir)
            .Count(f => ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));
    }

    private int CountLabels(string dir)
    {
        if (!Directory.Exists(dir)) return 0;
        return Directory.GetFiles(dir, "*.txt").Length;
    }

    private int CountMatchedAndAnnotations(string imgDir, string lblDir, Dictionary<int, int> classCounts)
    {
        if (!Directory.Exists(imgDir)) return 0;

        int matched = 0;
        var imageFiles = Directory.GetFiles(imgDir)
            .Where(f => ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));

        foreach (var imgPath in imageFiles)
        {
            string baseName = Path.GetFileNameWithoutExtension(imgPath);
            string lblPath = Path.Combine(lblDir, baseName + ".txt");

            if (File.Exists(lblPath))
            {
                matched++;
                // Parse annotations to count per-class
                try
                {
                    foreach (var line in File.ReadLines(lblPath))
                    {
                        var trimmed = line.Trim();
                        if (string.IsNullOrEmpty(trimmed)) continue;
                        var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 5 && int.TryParse(parts[0], out int classId))
                        {
                            classCounts.TryGetValue(classId, out int current);
                            classCounts[classId] = current + 1;
                        }
                    }
                }
                catch
                {
                    // skip corrupted label files
                }
            }
        }

        return matched;
    }

    private void CollectPairs(string imgDir, string lblDir, List<(string ImagePath, string? LabelPath)> pairs)
    {
        if (!Directory.Exists(imgDir)) return;

        var imageFiles = Directory.GetFiles(imgDir)
            .Where(f => ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));

        foreach (var imgPath in imageFiles)
        {
            string baseName = Path.GetFileNameWithoutExtension(imgPath);
            string lblPath = Path.Combine(lblDir, baseName + ".txt");
            pairs.Add((imgPath, File.Exists(lblPath) ? lblPath : null));
        }
    }

    private static void SafeMove(string source, string dest)
    {
        if (string.Equals(source, dest, StringComparison.OrdinalIgnoreCase)) return;
        if (File.Exists(dest)) File.Delete(dest);
        File.Move(source, dest);
    }
}
