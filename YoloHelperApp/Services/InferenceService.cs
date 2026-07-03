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
    }

    public InferenceResult RunInference(string imagePath, string modelPath, string task)
    {
        var output = new InferenceResult();
        if (!File.Exists(imagePath) || !File.Exists(modelPath))
            return output;

        using var mat = Cv2.ImRead(imagePath);
        if (mat.Empty()) return output;

        // Create ONNX Runtime context
        using var context = new OnnxRuntimeContext(modelPath, InferenceBackend.Cuda);

        // Fix dynamic batch sizes in the metadata to prevent NeuroModFlowNet from crashing
        var inShape = context.ModelInputShapes[context.PrimaryInputName];
        if (inShape[0] < 0) inShape[0] = 1;
        long h = inShape[2] > 0 ? inShape[2] : 640;
        long w = inShape[3] > 0 ? inShape[3] : 640;
        
        var outShape = context.ModelOutputShapes[context.PrimaryOutputName];
        if (outShape[0] < 0) outShape[0] = 1;
        
        context.InitInputPersistentValue(context.PrimaryInputName, new long[] { 1, 3, h, w });
        context.InitOutputPersistentValue(context.PrimaryOutputName, outShape);

        // Resize image with letterbox for accurate coordinates
        using var resized = NeuroModFlowNet.ONNX.Visualizer.MatPreprocessingExtensions.Letterbox(mat, (int)w, (int)h, out var info);

        if (task.Equals("detect", StringComparison.OrdinalIgnoreCase))
        {
            using var runner = YoloBoxFactory.CreateRunner<IDetectionResult<YoloBox>>(context);
            var result = runner.Predict(resized);
            var batch = result.GetBatch(0);
            
            // Draw boxes directly on the original image using visualizer
            var boxes = batch.ToArray();
            NeuroModFlowNet.ONNX.Visualizer.BoxPainter.DrawBox(mat, boxes, info);
            
            foreach (var box in batch)
            {
                output.Predictions.Add(new Prediction
                {
                    Label = $"Class {box.Class}",
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
            using var runner = YoloObbFactory.CreateRunner<IDetectionResult<YoloObb>>(context);
            var result = runner.Predict(resized);
            var batch = result.GetBatch(0);
            
            var boxes = batch.ToArray();
            NeuroModFlowNet.ONNX.Visualizer.ObbPainter.DrawObb(mat, boxes, info, 1f, 1f);

            foreach (var obb in batch)
            {
                output.Predictions.Add(new Prediction
                {
                    Label = $"Class {obb.Class}",
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
            using var runner = YoloPoseFactory.CreateRunner(context);
            var result = runner.Predict(resized);
            var batch = result.GetBatch(0);
            
            // We need to use GetBatch(0) and then convert to Array for visualizer if possible
            // We will just try if DrawPose accepts batch
            NeuroModFlowNet.ONNX.Visualizer.YoloPosePainter.DrawPose(mat, batch.ToArray(), info, 1f, 1f);

            foreach (var pose in batch)
            {
                var p = new Prediction
                {
                    Label = "Person",
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
        else if (task.Equals("seg", StringComparison.OrdinalIgnoreCase))
        {
            // Placeholder: segmentation support
        }

        output.RenderedImage = mat.ToBytes(".jpg");
        return output;
    }
}
