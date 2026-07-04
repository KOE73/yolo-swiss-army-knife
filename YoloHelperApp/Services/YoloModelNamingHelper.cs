using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YoloHelperApp.Models;

namespace YoloHelperApp.Services;

public static class YoloModelNamingHelper
{
    public static string GenerateModelName(int version, string sizeChar, string task)
    {
        string t = task?.ToLower() ?? "";
        string suffix = t switch
        {
            "obb" => "-obb",
            "pose" => "-pose",
            "segment" => "-seg",
            "classify" => "-cls",
            _ => "" // detect uses no suffix by default
        };

        // YOLO naming conventions have slightly changed across versions,
        // but typically it's yolovN or yolo11.
        string prefix = version switch
        {
            8 => "yolov8",
            9 => "yolov9",
            10 => "yolov10",
            11 => "yolo11",
            _ => $"yolo{version}" // generic fallback for v12+ or others
        };

        return $"{prefix}{sizeChar}{suffix}.pt";
    }

    // ── Exported model naming ────────────────────────────────────────────
    // Single source of truth for exported file names so that export,
    // model pickers, and post-export filters stay in sync.

    /// <param name="opset">null for formats where opset does not apply (non-ONNX)</param>
    /// <param name="batchLabel">"b{N}" for static batch or "dyn" for dynamic</param>
    public static string GetExportedFileName(string stem, int imgsz, int? opset, string precision, string batchLabel, string extension)
    {
        string opPart = opset.HasValue ? $"_op{opset.Value}" : "";
        return $"{stem}_img{imgsz}{opPart}_{precision.ToLowerInvariant()}_{batchLabel}{extension}";
    }

    // The auto-generated test model used by Inference Preview: opset=12, fp32, batch=1,
    // imgsz matches the training resolution (see CreateAutoTestProfile)
    private static readonly System.Text.RegularExpressions.Regex AutoTestPattern =
        new(@"_img\d+_op12_fp32_b1$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    public static bool IsAutoTestModel(string filePath)
    {
        string name = Path.GetFileNameWithoutExtension(filePath);
        return AutoTestPattern.IsMatch(name);
    }

    /// <param name="task">Training task (from args.yaml) — used as the output file stem
    /// so the model name says what it is, e.g. "obb_img256_op12_fp32_b1.onnx".</param>
    /// <param name="imgsz">Training resolution (from the run's args.yaml) so the test model
    /// matches what the network was trained on; rounded to a multiple of 32.</param>
    public static ExportProfile CreateAutoTestProfile(string task, int imgsz, bool injectByteBgr, string onnxToolsPath = "")
    {
        int normalized = Math.Max(32, (int)Math.Round(imgsz / 32.0) * 32);
        return new ExportProfile
        {
            Name = "Auto Inference ONNX",
            Format = "onnx",
            Opsets = new List<int> { 12 },
            BatchSizes = new List<string> { "1" },
            Precisions = new List<string> { "FP32" },
            ImgSizes = new List<int> { normalized },
            Simplify = true,
            // NeuroModFlowNet extractors (Yolo*Nms*Extractor) require embedded NMS —
            // a raw YOLO head output decodes as garbage
            Nms = true,
            InjectByteBGR = injectByteBgr,
            OnnxToolsPath = onnxToolsPath,
            OutputStem = string.IsNullOrWhiteSpace(task) ? "detect" : task.ToLowerInvariant()
        };
    }

    /// <summary>Collects ONNX files belonging to a run: weights/, ExportedModels/ and the run root.</summary>
    public static List<string> CollectOnnxFiles(string runPath)
    {
        var onnxFiles = new List<string>();
        if (string.IsNullOrWhiteSpace(runPath) || !Directory.Exists(runPath)) return onnxFiles;

        string weightsPath = Path.Combine(runPath, "weights");
        string exportedModelsPath = Path.Combine(runPath, "ExportedModels");

        if (Directory.Exists(weightsPath))
            onnxFiles.AddRange(Directory.GetFiles(weightsPath, "*.onnx"));
        if (Directory.Exists(exportedModelsPath))
            onnxFiles.AddRange(Directory.GetFiles(exportedModelsPath, "*.onnx"));
        onnxFiles.AddRange(Directory.GetFiles(runPath, "*.onnx"));

        return onnxFiles.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
