using YoloHelperApp.Models;
using YoloHelperApp.Services;

namespace YoloHelperApp.Tests;

public class YoloModelNamingHelperTests
{
    [Theory]
    [InlineData(8, "n", "detect", "yolov8n.pt")]
    [InlineData(11, "s", "obb", "yolo11s-obb.pt")]
    [InlineData(11, "m", "pose", "yolo11m-pose.pt")]
    [InlineData(11, "l", "segment", "yolo11l-seg.pt")]
    [InlineData(11, "x", "classify", "yolo11x-cls.pt")]
    [InlineData(12, "n", "detect", "yolo12n.pt")]
    public void GenerateModelName_FollowsUltralyticsConventions(int version, string size, string task, string expected)
    {
        Assert.Equal(expected, YoloModelNamingHelper.GenerateModelName(version, size, task));
    }

    [Fact]
    public void GetExportedFileName_Onnx_IncludesOpset()
    {
        string name = YoloModelNamingHelper.GetExportedFileName("best", 640, 12, "fp32", "b1", ".onnx");
        Assert.Equal("best_img640_op12_fp32_b1.onnx", name);
    }

    [Fact]
    public void GetExportedFileName_NonOnnx_OmitsOpset()
    {
        string name = YoloModelNamingHelper.GetExportedFileName("best", 320, null, "fp16", "dyn", ".engine");
        Assert.Equal("best_img320_fp16_dyn.engine", name);
    }

    [Fact]
    public void GetExportedFileName_RespectsWeightsStem()
    {
        // Exporting last.pt must not produce best_* names
        string name = YoloModelNamingHelper.GetExportedFileName("last", 640, 18, "fp32", "b4", ".onnx");
        Assert.StartsWith("last_", name);
    }

    [Fact]
    public void IsAutoTestModel_MatchesGeneratedTestProfileOutput_AnyStemAndImgsz()
    {
        // The auto test profile is op12/fp32/batch=1 with the training imgsz;
        // its stem is the task name (obb, detect, pose...)
        foreach (var (stem, imgsz) in new[] { ("obb", 256), ("detect", 640), ("pose", 1280), ("best", 640) })
        {
            string generated = YoloModelNamingHelper.GetExportedFileName(stem, imgsz, 12, "fp32", "b1", ".onnx");
            Assert.True(YoloModelNamingHelper.IsAutoTestModel(generated));
        }

        Assert.False(YoloModelNamingHelper.IsAutoTestModel("best_img640_op18_fp32_b1.onnx"));
        Assert.False(YoloModelNamingHelper.IsAutoTestModel("best_img640_op12_fp16_b1.onnx"));
        Assert.False(YoloModelNamingHelper.IsAutoTestModel("best_img640_op12_fp32_b4.onnx"));
        Assert.False(YoloModelNamingHelper.IsAutoTestModel("best.onnx"));
    }

    [Fact]
    public void CreateAutoTestProfile_UsesTaskAndTrainingImgsz()
    {
        var p = YoloModelNamingHelper.CreateAutoTestProfile("obb", 256, injectByteBgr: false);
        Assert.Equal("onnx", p.Format);
        Assert.Equal(new[] { 12 }, p.Opsets);
        Assert.Equal(new[] { "1" }, p.BatchSizes);
        Assert.Equal(new[] { "FP32" }, p.Precisions);
        Assert.Equal(new[] { 256 }, p.ImgSizes);
        Assert.Equal("obb", p.OutputStem);
        // NeuroModFlowNet extractors require NMS-embedded models
        Assert.True(p.Nms);

        // Generated name carries the task, not "best"
        string name = YoloModelNamingHelper.GetExportedFileName(p.OutputStem, 256, 12, "fp32", "b1", ".onnx");
        Assert.Equal("obb_img256_op12_fp32_b1.onnx", name);
        Assert.True(YoloModelNamingHelper.IsAutoTestModel(name));

        // Odd sizes are normalized to a multiple of 32
        Assert.Equal(new[] { 256 }, YoloModelNamingHelper.CreateAutoTestProfile("detect", 250, false).ImgSizes);
    }
}

public class AugmentationDefaultsTests
{
    [Fact]
    public void AugmentationProfile_DefaultsMatchUltralytics()
    {
        // https://docs.ultralytics.com/modes/train/#augmentation-settings-and-hyperparameters
        var p = new AugmentationProfile();
        Assert.Equal(0.015, p.HsvH);
        Assert.Equal(0.7, p.HsvS);
        Assert.Equal(0.4, p.HsvV);
        Assert.Equal(0.0, p.Degrees);
        Assert.Equal(0.1, p.Translate);
        Assert.Equal(0.5, p.Scale);
        Assert.Equal(0.0, p.Shear);
        Assert.Equal(0.0, p.Perspective);
        Assert.Equal(0.0, p.Flipud);
        Assert.Equal(0.5, p.Fliplr);
        Assert.Equal(1.0, p.Mosaic);
        Assert.Equal(0.0, p.Mixup);
        Assert.Equal(0.0, p.CopyPaste);
    }
}
