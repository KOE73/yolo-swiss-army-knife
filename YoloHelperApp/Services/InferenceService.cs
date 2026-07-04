using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using OpenCvSharp;
using NeuroModFlowNet.ONNX;

namespace YoloHelperApp.Services;

public class InferenceService
{
    public class Prediction
    {
        public string Label { get; set; } = "object";
        public float Score { get; set; }
        public float X { get; set; } // Center X or Min X
        public float Y { get; set; } // Center Y or Min Y
        public float W { get; set; } // Width
        public float H { get; set; } // Height
        public float Angle { get; set; } // OBB angle in radians
        public List<(float X, float Y, float V)> Keypoints { get; set; } = new();
        public List<(float X, float Y)> Polygon { get; set; } = new();
    }

    public class InferenceResult
    {
        public List<Prediction> Predictions { get; set; } = new();
        public byte[]? RenderedImage { get; set; }
        public string Backend { get; set; } = "";
        public string? Warning { get; set; }

        // Diagnostics: which decoding pipeline actually ran, so the user can verify
        // that the model type matches the training task
        public string FactoryUsed { get; set; } = "";
        public string InputShape { get; set; } = "";
        public string OutputShape { get; set; } = "";
        public int TotalRaw { get; set; }     // detections before confidence filtering
        public int Kept { get; set; }         // detections above the threshold
    }

    private static string ClassLabel(int classId, IReadOnlyDictionary<int, string>? classNames)
        => classNames != null && classNames.TryGetValue(classId, out var name) ? name : $"class_{classId}";

    public InferenceResult RunInference(string imagePath, string modelPath, string task,
        IReadOnlyDictionary<int, string>? classNames = null, float confThreshold = 0.25f)
    {
        // The visualizer formats scores ("{Score:P0}") with the thread culture; ru-RU
        // inserts non-breaking spaces that OpenCV Hershey fonts render as '?'
        System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

        var output = new InferenceResult();
        if (!File.Exists(imagePath) || !File.Exists(modelPath))
            return output;

        using var mat = Cv2.ImRead(imagePath);
        if (mat.Empty()) return output;

        // Create ONNX Runtime context: try CUDA first, fall back to CPU
        OnnxRuntimeContext? context = null;
        try
        {
            context = new OnnxRuntimeContext(modelPath, InferenceBackend.Cuda);
            output.Backend = "CUDA";
        }
        catch (Exception)
        {
            context = new OnnxRuntimeContext(modelPath, InferenceBackend.Cpu);
            output.Backend = "CPU";
            output.Warning = "CUDA is unavailable, running inference on CPU.";
        }

        using var _ = context;

        // Fix dynamic batch sizes in the metadata to prevent NeuroModFlowNet from crashing
        var inShape = context.ModelInputShapes[context.PrimaryInputName];
        if (inShape[0] < 0) inShape[0] = 1;
        long h = inShape[2] > 0 ? inShape[2] : 640;
        long w = inShape[3] > 0 ? inShape[3] : 640;

        var outShape = context.ModelOutputShapes[context.PrimaryOutputName];
        if (outShape[0] < 0) outShape[0] = 1;

        context.InitInputPersistentValue(context.PrimaryInputName, new long[] { 1, 3, h, w });
        context.InitOutputPersistentValue(context.PrimaryOutputName, outShape);

        output.InputShape = $"[1,3,{h},{w}]";
        output.OutputShape = $"[{string.Join(",", outShape)}]";

        // NeuroModFlowNet extractors expect NMS-embedded output like [1, maxDet, 6..8].
        // A raw YOLO head is [1, channels, anchors] (small middle dim, huge last dim) —
        // decoding it produces garbage boxes, so warn loudly.
        if (outShape.Length == 3 && outShape[1] < outShape[2] && outShape[2] > 64)
        {
            output.Warning = $"Model output {output.OutputShape} looks like a RAW YOLO head (no embedded NMS). " +
                             "Regenerate the test ONNX — it must be exported with nms=True.";
        }

        // Resize image with letterbox for accurate coordinates
        using var resized = NeuroModFlowNet.ONNX.Visualizer.MatPreprocessingExtensions.Letterbox(mat, (int)w, (int)h, out var info);

        if (task.Equals("detect", StringComparison.OrdinalIgnoreCase))
        {
            output.FactoryUsed = "YoloBoxFactory (detect)";
            using var runner = YoloBoxFactory.CreateRunner<IDetectionResult<YoloBox>>(context);
            var result = runner.Predict(resized);
            var all = result.GetBatch(0).ToArray();
            var boxes = all.Where(b => b.Score >= confThreshold).ToArray();
            output.TotalRaw = all.Length;
            output.Kept = boxes.Length;

            // Draw only confident boxes directly on the original image using visualizer
            NeuroModFlowNet.ONNX.Visualizer.BoxPainter.DrawBox(mat, boxes, info);

            foreach (var box in boxes)
            {
                output.Predictions.Add(new Prediction
                {
                    Label = ClassLabel(box.Class, classNames),
                    Score = box.Score,
                    X = box.X,
                    Y = box.Y,
                    W = box.W,
                    H = box.H
                });
            }
        }
        else if (task.Equals("obb", StringComparison.OrdinalIgnoreCase))
        {
            output.FactoryUsed = "YoloObbFactory (obb)";
            using var runner = YoloObbFactory.CreateRunner<IDetectionResult<YoloObb>>(context);
            var result = runner.Predict(resized);
            var all = result.GetBatch(0).ToArray();
            var boxes = all.Where(b => b.Score >= confThreshold).ToArray();
            output.TotalRaw = all.Length;
            output.Kept = boxes.Length;

            NeuroModFlowNet.ONNX.Visualizer.ObbPainter.DrawObb(mat, boxes, info, 1f, 1f);

            foreach (var obb in boxes)
            {
                output.Predictions.Add(new Prediction
                {
                    Label = ClassLabel(obb.Class, classNames),
                    Score = obb.Score,
                    X = obb.X,
                    Y = obb.Y,
                    W = obb.W,
                    H = obb.H,
                    Angle = obb.Angle
                });
            }
        }
        else if (task.Equals("pose", StringComparison.OrdinalIgnoreCase))
        {
            output.FactoryUsed = "YoloPoseFactory (pose)";
            using var runner = YoloPoseFactory.CreateRunner(context);
            var result = runner.Predict(resized);
            var all = result.GetBatch(0).ToArray();
            var poses = all.Where(p => p.Score >= confThreshold).ToArray();
            output.TotalRaw = all.Length;
            output.Kept = poses.Length;

            NeuroModFlowNet.ONNX.Visualizer.YoloPosePainter.DrawPose(mat, poses, info, 1f, 1f);

            foreach (var pose in poses)
            {
                var p = new Prediction
                {
                    // Pose models are single-class; use the class name if provided
                    Label = ClassLabel(0, classNames),
                    Score = pose.Score,
                    X = pose.X,
                    Y = pose.Y,
                    W = pose.W,
                    H = pose.H
                };
                if (pose.Keypoints != null)
                {
                    foreach (var kp in pose.Keypoints)
                    {
                        p.Keypoints.Add((kp.X, kp.Y, kp.V));
                    }
                }
                output.Predictions.Add(p);
            }
        }
        else
        {
            // seg / classify are not supported yet — be explicit instead of silently
            // rendering the untouched source image (see AGENTS.md §5)
            output.Warning = $"Task '{task}' is not supported by Inference Preview yet.";
        }

        output.RenderedImage = mat.ToBytes(".jpg");
        return output;
    }
}
