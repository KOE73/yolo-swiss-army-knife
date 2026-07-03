using System;
using System.Collections.Generic;

namespace YoloHelperApp.Models;

public class ExportProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "ONNX Export Config";
    public bool IsActive { get; set; } = true;

    // Parameters
    public string Format { get; set; } = "onnx"; // onnx, engine, openvino, etc.
    public List<int> Oplets { get; set; } = new() { 18 }; // e.g. 12, 13, 18, etc.
    public List<string> BatchSizes { get; set; } = new() { "1", "dynamic" }; // Can contain numbers or "dynamic"
    public List<int> ImgSizes { get; set; } = new() { 640 }; // Can contain numbers e.g. 320, 640
    public List<string> Precisions { get; set; } = new() { "FP32" }; // FP32, FP16, INT8
    public bool Simplify { get; set; } = true;
    public bool Nms { get; set; } = false;
    public string Device { get; set; } = "0"; // 0, cpu
    public bool InjectByteBGR { get; set; } = false;

    public string Summarize()
    {
        var opsetsStr = string.Join(",", Oplets);
        var batchesStr = string.Join(",", BatchSizes);
        var precStr = string.Join(",", Precisions);
        var byteBgr = InjectByteBGR ? "+ByteBGR" : "";
        var dynamicStr = BatchSizes.Contains("dynamic") ? "Dynamic" : "Static";
        return $"{Name} ({Format.ToUpper()}) (Opsets:[{opsetsStr}] Batches:[{batchesStr}] {precStr} {dynamicStr} {byteBgr})";
    }
}
