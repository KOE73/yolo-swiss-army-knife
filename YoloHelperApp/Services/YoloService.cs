using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using YoloHelperApp.Models;

namespace YoloHelperApp.Services;

public class YoloService
{
    public event Action<string>? OnLogReceived;

    public Task RunTrainAsync(string projectFolder, ProjectSettings settings, AugmentationProfile? augmentationProfile = null)
    {
        return Task.Run(() =>
        {
            string cfgArg = "";
            if (augmentationProfile != null)
            {
                var yamlObj = new
                {
                    hsv_h = augmentationProfile.HsvH,
                    hsv_s = augmentationProfile.HsvS,
                    hsv_v = augmentationProfile.HsvV,
                    degrees = augmentationProfile.Degrees,
                    translate = augmentationProfile.Translate,
                    scale = augmentationProfile.Scale,
                    shear = augmentationProfile.Shear,
                    perspective = augmentationProfile.Perspective,
                    flipud = augmentationProfile.Flipud,
                    fliplr = augmentationProfile.Fliplr,
                    mosaic = augmentationProfile.Mosaic,
                    mixup = augmentationProfile.Mixup,
                    copy_paste = augmentationProfile.CopyPaste
                };

                var serializer = new YamlDotNet.Serialization.SerializerBuilder()
                    .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
                    .Build();
                
                string yamlContent = serializer.Serialize(yamlObj);
                string yamlPath = Path.Combine(projectFolder, "train_hyp.yaml");
                File.WriteAllText(yamlPath, yamlContent);
                cfgArg = $" cfg=train_hyp.yaml";
                Log($"Generated hyperparameter config at {yamlPath}");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c yolo task={settings.Task} mode=train model={settings.ModelName} data=data.yaml epochs={settings.Epochs} imgsz={settings.ImageSize} batch={settings.BatchSize} device={settings.Device} workers={settings.Workers} project={settings.ProjectName} name={settings.RunName}{cfgArg}",
                WorkingDirectory = projectFolder,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            // Set up MLFlow variables if enabled
            if (settings.UseMlflow)
            {
                startInfo.EnvironmentVariables["AWS_ACCESS_KEY_ID"] = settings.AwsAccessKeyId;
                startInfo.EnvironmentVariables["AWS_SECRET_ACCESS_KEY"] = settings.AwsSecretAccessKey;
                startInfo.EnvironmentVariables["MLFLOW_S3_ENDPOINT_URL"] = settings.MlflowS3EndpointUrl;
                startInfo.EnvironmentVariables["MLFLOW_TRACKING_URI"] = settings.MlflowTrackingUri;
                // Enable mlflow in yolo settings first
                Log("Enabling mlflow in yolo settings...");
                RunCommandSilently("yolo settings mlflow=True", projectFolder);
            }
            else
            {
                RunCommandSilently("yolo settings mlflow=False", projectFolder);
            }

            Log($"Starting YOLO training: {startInfo.Arguments}");
            ExecuteProcess(startInfo);
        });
    }

    public Task RunExportAsync(string projectFolder, string bestPtPath, ExportProfile profile)
    {
        return Task.Run(() =>
        {
            var precisions = profile.Precisions != null && profile.Precisions.Count > 0 ? profile.Precisions : new List<string> { "FP32" };
            string format = string.IsNullOrWhiteSpace(profile.Format) ? "onnx" : profile.Format.ToLower();
            var imgSizes = profile.ImgSizes != null && profile.ImgSizes.Count > 0 ? profile.ImgSizes : new List<int> { 640 };

            foreach (var imgsz in imgSizes)
            {
                foreach (var opset in profile.Oplets)
                {
                    foreach (var batchStr in profile.BatchSizes)
                    {
                        foreach (var precisionStr in precisions)
                        {
                            bool isDynamic = batchStr.Equals("dynamic", StringComparison.OrdinalIgnoreCase);
                            string batchArg = isDynamic ? "1" : batchStr;
                            string dynamicArg = isDynamic ? "dynamic=True" : "dynamic=False";
                            
                            bool isFp16 = precisionStr.Equals("FP16", StringComparison.OrdinalIgnoreCase);
                            bool isInt8 = precisionStr.Equals("INT8", StringComparison.OrdinalIgnoreCase);
                            string halfArg = isFp16 ? "half=True" : "half=False";
                            string int8Arg = isInt8 ? "int8=True" : "int8=False";
                            
                            string simplifyArg = profile.Simplify ? "simplify=True" : "simplify=False";
                            string nmsArg = profile.Nms ? "nms=True" : "nms=False";

                            string outputDir = Path.GetDirectoryName(bestPtPath) ?? projectFolder;
                            Directory.CreateDirectory(outputDir);

                            string precisionName = precisionStr.ToLower();
                            string dynamicName = isDynamic ? "dyn" : $"b{batchStr}";
                            
                            string expectedExt = format switch {
                                "onnx" => ".onnx",
                                "engine" => ".engine",
                                "torchscript" => ".torchscript",
                                "openvino" => "_openvino_model",
                                "coreml" => ".mlpackage",
                                "saved_model" => "_saved_model",
                                "pb" => ".pb",
                                "tflite" => ".tflite",
                                "edgetpu" => "_edgetpu.tflite",
                                "tfjs" => "_web_model",
                                "paddle" => "_paddle_model",
                                "ncnn" => "_ncnn_model",
                                _ => $".{format}"
                            };

                            string weightsDir = Path.GetDirectoryName(bestPtPath) ?? "";
                            string expectedOutput = Path.Combine(weightsDir, "best" + expectedExt);
                            
                            // Clean up previous if exists
                            if (File.Exists(expectedOutput)) File.Delete(expectedOutput);
                            else if (Directory.Exists(expectedOutput)) Directory.Delete(expectedOutput, true);

                            Log($"Exporting Opset {opset}, Batch {batchStr}, Precision {precisionName}, Format {format}, Imgsz {imgsz}...");
                            
                            string args = $"export model=\"{bestPtPath}\" format={format} imgsz={imgsz} opset={opset} batch={batchArg} {dynamicArg} {halfArg} {int8Arg} {simplifyArg} {nmsArg} device={profile.Device}";
                            var startInfo = new ProcessStartInfo
                            {
                                FileName = "yolo",
                                Arguments = args,
                                WorkingDirectory = projectFolder,
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                CreateNoWindow = true
                            };

                            ExecuteProcess(startInfo);

                            string targetName = $"best_img{imgsz}_op{opset}_{precisionName}_{dynamicName}{expectedExt}";
                            string targetPath = Path.Combine(outputDir, targetName);

                            if (File.Exists(expectedOutput))
                            {
                                if (File.Exists(targetPath)) File.Delete(targetPath);
                                File.Move(expectedOutput, targetPath);
                                Log($"[SUCCESS] Model exported to: {targetPath}");

                                if (profile.InjectByteBGR && format == "onnx")
                                {
                                    InjectByteBgr(targetPath, null);
                                }
                            }
                            else if (Directory.Exists(expectedOutput))
                            {
                                if (Directory.Exists(targetPath)) Directory.Delete(targetPath, true);
                                Directory.Move(expectedOutput, targetPath);
                                Log($"[SUCCESS] Model exported to directory: {targetPath}");
                            }
                            else
                            {
                                Log($"[WARNING] Expected exported model not found at: {expectedOutput}");
                            }
                        }
                    }
                }
            }
        });
    }

    public void InjectByteBgr(string onnxPath, string? toolsPath)
    {
        string exePath = toolsPath ?? "";
        if (string.IsNullOrEmpty(exePath))
        {
            // Try to find in PATH first
            var envPath = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrWhiteSpace(envPath))
            {
                var paths = envPath.Split(Path.PathSeparator);
                foreach (var p in paths)
                {
                    var testPath = Path.Combine(p, "NeuroModFlowNet.ONNX.Tools.exe");
                    if (File.Exists(testPath))
                    {
                        exePath = testPath;
                        break;
                    }
                    testPath = Path.Combine(p, "NeuroModFlowNet.ONNX.Tools");
                    if (File.Exists(testPath))
                    {
                        exePath = testPath;
                        break;
                    }
                }
            }
        }

        if (string.IsNullOrEmpty(exePath))
        {
            Log($"[ERROR] ByteBGR injection failed: NeuroModFlowNet.ONNX.Tools.exe not found. " +
                $"Please add the directory containing the tool to your system PATH environment variable.");
            return;
        }

        if (!File.Exists(exePath))
        {
            Log($"[ERROR] ByteBGR injection failed: Tool not found at specified path '{exePath}'.");
            return;
        }

        Log($"[ByteBGR] Injecting ByteBGR preprocessing node to ONNX model using: {exePath}");
        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = $"inject ByteBGR \"{onnxPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        ExecuteProcess(startInfo);
        Log($"[ByteBGR] Injection completed for: {onnxPath}");
    }

    private void ExecuteProcess(ProcessStartInfo startInfo)
    {
        using var process = Process.Start(startInfo);
        if (process == null)
        {
            Log("[ERROR] Failed to start process.");
            return;
        }

        process.OutputDataReceived += (s, e) => { if (e.Data != null) Log(e.Data); };
        process.ErrorDataReceived += (s, e) => { if (e.Data != null) Log($"[ERROR] {e.Data}"); };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        process.WaitForExit();
    }

    private void RunCommandSilently(string command, string workingDir)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c {command}",
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var process = Process.Start(startInfo);
        process?.WaitForExit();
    }

    private void Log(string message)
    {
        OnLogReceived?.Invoke($"{DateTime.Now:HH:mm:ss} - {message}");
    }
}
