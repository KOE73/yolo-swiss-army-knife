using System;
using System.Globalization;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using YoloHelperApp.Models;

namespace YoloHelperApp.Services;

public class YoloService
{
    public event Action<string>? OnLogReceived;

    private readonly object _processLock = new();
    private Process? _currentProcess;

    /// <summary>Kills the currently running yolo process (train or export), including children.</summary>
    public void StopCurrentProcess()
    {
        lock (_processLock)
        {
            if (_currentProcess == null) return;
            try
            {
                if (!_currentProcess.HasExited)
                {
                    _currentProcess.Kill(entireProcessTree: true);
                    Log("[STOP] Process terminated by user.");
                }
            }
            catch (Exception ex)
            {
                Log($"[ERROR] Failed to stop process: {ex.Message}");
            }
        }
    }

    private static string Inv(double value) => value.ToString(CultureInfo.InvariantCulture);

    public Task RunTrainAsync(string projectFolder, ProjectSettings settings, AugmentationProfile? augmentationProfile = null)
    {
        return Task.Run(() =>
        {
            var train = settings.Train;
            // Ultralytics expects device=cpu, not a negative GPU id
            string deviceArg = train.Device < 0 ? "cpu" : train.Device.ToString();

            // Augmentation parameters are passed as CLI overrides (snake_case, invariant culture).
            string augArgs = "";
            if (augmentationProfile != null)
            {
                var p = augmentationProfile;
                augArgs =
                    $" hsv_h={Inv(p.HsvH)} hsv_s={Inv(p.HsvS)} hsv_v={Inv(p.HsvV)}" +
                    $" degrees={Inv(p.Degrees)} translate={Inv(p.Translate)} scale={Inv(p.Scale)}" +
                    $" shear={Inv(p.Shear)} perspective={Inv(p.Perspective)}" +
                    $" flipud={Inv(p.Flipud)} fliplr={Inv(p.Fliplr)}" +
                    $" mosaic={Inv(p.Mosaic)} mixup={Inv(p.Mixup)} copy_paste={Inv(p.CopyPaste)}";
                Log($"Using augmentation profile '{p.ProfileName}'.");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c yolo task={train.Task} mode=train model={train.ModelName} data=data.yaml epochs={train.Epochs} imgsz={train.ImageSize} batch={train.BatchSize} device={deviceArg} workers={train.Workers} project={train.ProjectName} name={train.RunName}{augArgs}",
                WorkingDirectory = projectFolder,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            // Set up MLFlow variables if enabled
            if (settings.Mlflow.Enabled)
            {
                var mlflow = settings.Mlflow;
                if (!string.IsNullOrWhiteSpace(mlflow.AwsAccessKeyId))
                    startInfo.EnvironmentVariables["AWS_ACCESS_KEY_ID"] = mlflow.AwsAccessKeyId;
                if (!string.IsNullOrWhiteSpace(mlflow.AwsSecretAccessKey))
                    startInfo.EnvironmentVariables["AWS_SECRET_ACCESS_KEY"] = mlflow.AwsSecretAccessKey;
                if (!string.IsNullOrWhiteSpace(mlflow.S3EndpointUrl))
                    startInfo.EnvironmentVariables["MLFLOW_S3_ENDPOINT_URL"] = mlflow.S3EndpointUrl;
                if (!string.IsNullOrWhiteSpace(mlflow.TrackingUri))
                    startInfo.EnvironmentVariables["MLFLOW_TRACKING_URI"] = mlflow.TrackingUri;
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

    // ── Export format capabilities (see docs/ExportFormats.md) ─────────────

    private static readonly HashSet<string> HalfFormats = new(StringComparer.OrdinalIgnoreCase)
        { "onnx", "openvino", "engine", "coreml", "tflite", "ncnn" };
    private static readonly HashSet<string> Int8Formats = new(StringComparer.OrdinalIgnoreCase)
        { "openvino", "engine", "coreml", "saved_model", "tflite", "tfjs", "ncnn" };
    private static readonly HashSet<string> DynamicFormats = new(StringComparer.OrdinalIgnoreCase)
        { "onnx", "openvino", "engine" };
    private static readonly HashSet<string> SimplifyFormats = new(StringComparer.OrdinalIgnoreCase)
        { "onnx", "engine" };
    private static readonly HashSet<string> NmsFormats = new(StringComparer.OrdinalIgnoreCase)
        { "onnx", "openvino", "engine", "coreml", "saved_model", "tflite", "tfjs" };

    public static string GetExpectedExtension(string format) => format.ToLowerInvariant() switch
    {
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
        _ => $".{format.ToLowerInvariant()}"
    };

    public Task RunExportAsync(string projectFolder, string bestPtPath, ExportProfile profile)
    {
        return Task.Run(() =>
        {
            var precisions = profile.Precisions != null && profile.Precisions.Count > 0 ? profile.Precisions : new List<string> { "FP32" };
            string format = string.IsNullOrWhiteSpace(profile.Format) ? "onnx" : profile.Format.ToLower();
            var imgSizes = profile.ImgSizes != null && profile.ImgSizes.Count > 0 ? profile.ImgSizes : new List<int> { 640 };

            bool isOnnx = format == "onnx";
            // opset only applies to ONNX; for other formats a single pass is enough
            var opsets = isOnnx && profile.Opsets.Count > 0 ? profile.Opsets : new List<int> { 0 };

            // ultralytics names its output after the source .pt; our renamed target may
            // use a custom stem (e.g. the task name for auto test models)
            string ptStem = Path.GetFileNameWithoutExtension(bestPtPath);
            string outStem = string.IsNullOrWhiteSpace(profile.OutputStem) ? ptStem : profile.OutputStem;
            string expectedExt = GetExpectedExtension(format);
            string weightsDir = Path.GetDirectoryName(bestPtPath) ?? projectFolder;
            string outputDir = weightsDir;
            Directory.CreateDirectory(outputDir);

            var doneCombos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var imgsz in imgSizes)
            {
                foreach (var opset in opsets)
                {
                    foreach (var batchStr in profile.BatchSizes)
                    {
                        foreach (var precisionStr in precisions)
                        {
                            bool isDynamic = batchStr.Equals("dynamic", StringComparison.OrdinalIgnoreCase);
                            bool isFp16 = precisionStr.Equals("FP16", StringComparison.OrdinalIgnoreCase);
                            bool isInt8 = precisionStr.Equals("INT8", StringComparison.OrdinalIgnoreCase);

                            if (isFp16 && !HalfFormats.Contains(format))
                            {
                                Log($"[SKIP] FP16 is not supported for format '{format}'.");
                                continue;
                            }
                            if (isInt8 && !Int8Formats.Contains(format))
                            {
                                Log($"[SKIP] INT8 is not supported for format '{format}'.");
                                continue;
                            }
                            if (isDynamic && !DynamicFormats.Contains(format))
                            {
                                Log($"[SKIP] dynamic batch is not supported for format '{format}'.");
                                continue;
                            }
                            if (isOnnx && isFp16 && isDynamic)
                            {
                                Log("[SKIP] ONNX: half=True is incompatible with dynamic=True.");
                                continue;
                            }

                            string batchArg = isDynamic ? "1" : batchStr;
                            string precisionName = precisionStr.ToLower();
                            string dynamicName = isDynamic ? "dyn" : $"b{batchStr}";

                            string comboKey = $"{imgsz}|{(isOnnx ? opset : 0)}|{batchArg}|{isDynamic}|{precisionName}";
                            if (!doneCombos.Add(comboKey)) continue;

                            var args = new List<string>
                            {
                                "export",
                                $"model=\"{bestPtPath}\"",
                                $"format={format}",
                                $"imgsz={imgsz}",
                                $"batch={batchArg}"
                            };
                            if (isOnnx) args.Add($"opset={opset}");
                            if (DynamicFormats.Contains(format)) args.Add(isDynamic ? "dynamic=True" : "dynamic=False");
                            if (HalfFormats.Contains(format)) args.Add(isFp16 ? "half=True" : "half=False");
                            if (Int8Formats.Contains(format))
                            {
                                args.Add(isInt8 ? "int8=True" : "int8=False");
                                // INT8 requires a calibration dataset
                                if (isInt8 && File.Exists(Path.Combine(projectFolder, "data.yaml")))
                                    args.Add("data=data.yaml");
                            }
                            if (SimplifyFormats.Contains(format)) args.Add(profile.Simplify ? "simplify=True" : "simplify=False");
                            if (profile.Nms && NmsFormats.Contains(format)) args.Add("nms=True");
                            args.Add($"device={profile.Device}");

                            string expectedOutput = Path.Combine(weightsDir, ptStem + expectedExt);

                            // Clean up previous if exists
                            if (File.Exists(expectedOutput)) File.Delete(expectedOutput);
                            else if (Directory.Exists(expectedOutput)) Directory.Delete(expectedOutput, true);

                            Log($"Exporting Format {format}, Imgsz {imgsz}{(isOnnx ? $", Opset {opset}" : "")}, Batch {batchStr}, Precision {precisionName}...");

                            var startInfo = new ProcessStartInfo
                            {
                                FileName = "cmd.exe",
                                Arguments = "/c yolo " + string.Join(" ", args),
                                WorkingDirectory = projectFolder,
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                CreateNoWindow = true
                            };

                            ExecuteProcess(startInfo);

                            string targetName = YoloModelNamingHelper.GetExportedFileName(
                                outStem, imgsz, isOnnx ? opset : null, precisionName, dynamicName, expectedExt);
                            string targetPath = Path.Combine(outputDir, targetName);

                            if (File.Exists(expectedOutput))
                            {
                                if (File.Exists(targetPath)) File.Delete(targetPath);
                                File.Move(expectedOutput, targetPath);
                                Log($"[SUCCESS] Model exported to: {targetPath}");

                                if (profile.InjectByteBGR && isOnnx)
                                {
                                    InjectByteBgr(targetPath, string.IsNullOrWhiteSpace(profile.OnnxToolsPath) ? null : profile.OnnxToolsPath);
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
        else if (Directory.Exists(exePath))
        {
            // A directory was configured; look for the executable inside it
            string candidate = Path.Combine(exePath, "NeuroModFlowNet.ONNX.Tools.exe");
            exePath = File.Exists(candidate) ? candidate : Path.Combine(exePath, "NeuroModFlowNet.ONNX.Tools");
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

        lock (_processLock) { _currentProcess = process; }

        try
        {
            process.OutputDataReceived += (s, e) => { if (e.Data != null) Log(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) Log($"[ERROR] {e.Data}"); };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            process.WaitForExit();
        }
        finally
        {
            lock (_processLock) { _currentProcess = null; }
        }
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
