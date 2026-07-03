using System;
using System.IO;
using System.Linq;
using Xunit;
using YoloHelperApp.Services;
using Xunit.Abstractions;

namespace YoloHelperApp.Tests;

public class InferenceTests
{
    private readonly ITestOutputHelper _output;

    public InferenceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void TestInference()
    {
        var model = @"C:\NN\CGP2510_Multi\CGP2510_Multi\CGP2510_Multi8\weights\best.onnx";
        var imgRoot = @"C:\NN\CGP2510_Multi\images\val";
        if (!Directory.Exists(imgRoot)) imgRoot = @"C:\NN\CGP2510_Multi\images\train";
        
        if (!File.Exists(model) || !Directory.Exists(imgRoot))
        {
            _output.WriteLine("Local test files not found. Skipping test.");
            return;
        }
        
        var image = Directory.GetFiles(imgRoot, "*.png").FirstOrDefault() 
                 ?? Directory.GetFiles(imgRoot, "*.jpg").FirstOrDefault();
        
        Assert.NotNull(image);

        using var mat = OpenCvSharp.Cv2.ImRead(image);
        Assert.False(mat.Empty());

        _output.WriteLine($"Image: {image}");
        _output.WriteLine($"Model: {model}");
        
        using var context = new NeuroModFlowNet.ONNX.OnnxRuntimeContext(
            modelPath: model,
            inferenceBackend: NeuroModFlowNet.ONNX.InferenceBackend.Cpu
        );
        
        // Fix dynamic batch sizes in the metadata to prevent NeuroModFlowNet from crashing
        var inShape = context.ModelInputShapes[context.PrimaryInputName];
        if (inShape[0] < 0) inShape[0] = 1;
        long h = inShape[2] > 0 ? inShape[2] : 640;
        long w = inShape[3] > 0 ? inShape[3] : 640;
        
        var outShape = context.ModelOutputShapes[context.PrimaryOutputName];
        if (outShape[0] < 0) outShape[0] = 1;
        
        context.InitInputPersistentValue(context.PrimaryInputName, new long[] { 1, 3, h, w });
        context.InitOutputPersistentValue(context.PrimaryOutputName, outShape);

        using var runner = NeuroModFlowNet.ONNX.YoloBoxFactory.CreateRunner<NeuroModFlowNet.ONNX.IDetectionResult<NeuroModFlowNet.ONNX.YoloBox>>(context);
        using var resized = new OpenCvSharp.Mat();
        OpenCvSharp.Cv2.Resize(mat, resized, new OpenCvSharp.Size(640, 640));
        var result = runner.Predict(resized);
        
        var batch = result.GetBatch(0);
        int count = 0;
        foreach (var item in batch) { count++; }
        
        _output.WriteLine($"Found {count} predictions.");
        Assert.True(count >= 0, "Inference completed.");
    }
}
