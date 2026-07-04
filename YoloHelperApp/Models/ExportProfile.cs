using System;
using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace YoloHelperApp.Models;

public class ExportProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "ONNX Export Config";
    public bool IsActive { get; set; } = true;

    // Parameters
    public string Format { get; set; } = "onnx"; // onnx, engine, openvino, etc.
    public List<int> Opsets { get; set; } = new() { 18 }; // e.g. 12, 13, 18, etc. (ONNX only)
    public List<string> BatchSizes { get; set; } = new() { "1", "dynamic" }; // Can contain numbers or "dynamic"
    public List<int> ImgSizes { get; set; } = new() { 640 }; // Can contain numbers e.g. 320, 640
    public List<string> Precisions { get; set; } = new() { "FP32" }; // FP32, FP16, INT8
    public bool Simplify { get; set; } = true;
    // Embedded NMS by default: the NeuroModFlowNet inference pipeline expects it,
    // and YOLO26+ models ship NMS-embedded only
    public bool Nms { get; set; } = true;
    public string Device { get; set; } = "0"; // 0, cpu
    public bool InjectByteBGR { get; set; } = false;

    // Optional explicit path to NeuroModFlowNet.ONNX.Tools (falls back to PATH lookup).
    // Not persisted per-profile: filled from project settings before export.
    [YamlIgnore]
    public string OnnxToolsPath { get; set; } = "";

    // Optional override for the exported file name stem (defaults to the .pt file name).
    // Used by the auto test profile to name models like "obb_img256_op12_fp32_b1.onnx".
    [YamlIgnore]
    public string OutputStem { get; set; } = "";

    public string Summarize()
    {
        var opsetsStr = string.Join(",", Opsets);
        var batchesStr = string.Join(",", BatchSizes);
        var imgszStr = string.Join(",", ImgSizes);
        var precStr = string.Join(",", Precisions);
        var byteBgr = InjectByteBGR ? "+ByteBGR" : "";
        var dynamicStr = BatchSizes.Contains("dynamic") ? "Dynamic" : "Static";
        return $"{Name} ({Format.ToUpper()}) (Imgsz:[{imgszStr}] Opsets:[{opsetsStr}] Batches:[{batchesStr}] {precStr} {dynamicStr} {byteBgr})";
    }
}
