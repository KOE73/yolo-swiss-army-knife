using System;
using System.Collections.Generic;

namespace YoloHelperApp.Models;

public class TrainSettings
{
    public string ProjectName { get; set; } = "YoloProject";
    public string RunName { get; set; } = "run1";
    public string Task { get; set; } = "detect"; // detect, pose, obb, segment, classify
    public int ModelVersion { get; set; } = 11;
    public string ModelSizeCode { get; set; } = "n";
    public string ModelName { get; set; } = "yolo11n.pt";
    public int ImageSize { get; set; } = 640;
    public int Epochs { get; set; } = 100;
    public int BatchSize { get; set; } = -1; // -1 for auto
    public int Device { get; set; } = 0; // GPU ID, -1 maps to cpu
    public int Workers { get; set; } = 2;
}

public class MlflowSettings
{
    public bool Enabled { get; set; } = false;
    public string TrackingUri { get; set; } = "";
    public string S3EndpointUrl { get; set; } = "";
    public string AwsAccessKeyId { get; set; } = "";
    public string AwsSecretAccessKey { get; set; } = "";
}

public class AugmentationSettings
{
    public string SelectedProfile { get; set; } = "Default (Ultralytics)";
    public List<AugmentationProfile> Profiles { get; set; } = new();
}

public class ExportSettings
{
    public List<ExportProfile> Profiles { get; set; } = new();
}

public class ToolsSettings
{
    public string OnnxToolsPath { get; set; } = "";
}

/// <summary>
/// Structured project configuration (project.ysak, version 2).
/// Version 1 (flat) files are migrated automatically by ProjectService.
/// </summary>
public class ProjectSettings
{
    public int Version { get; set; } = 2;

    public TrainSettings Train { get; set; } = new();
    public MlflowSettings Mlflow { get; set; } = new();
    public AugmentationSettings Augmentation { get; set; } = new();
    public ExportSettings Export { get; set; } = new();
    public ToolsSettings Tools { get; set; } = new();

    // Post Export actions: scripts/commands executed with exported model paths
    public List<string> PostExports { get; set; } = new();
}

/// <summary>Flat legacy (v1) schema, used only for migration.</summary>
public class LegacyProjectSettingsV1
{
    public string ProjectName { get; set; } = "YoloProject";
    public string RunName { get; set; } = "run1";
    public string Task { get; set; } = "detect";
    public int ModelVersion { get; set; } = 11;
    public string ModelSizeCode { get; set; } = "n";
    public string ModelName { get; set; } = "yolo11n.pt";
    public int ImageSize { get; set; } = 640;
    public int Epochs { get; set; } = 100;
    public int BatchSize { get; set; } = -1;
    public int Device { get; set; } = 0;
    public int Workers { get; set; } = 2;
    public string AugmentationProfileName { get; set; } = "Default (Ultralytics)";
    public bool UseMlflow { get; set; } = false;
    public string MlflowTrackingUri { get; set; } = "";
    public string MlflowS3EndpointUrl { get; set; } = "";
    public string AwsAccessKeyId { get; set; } = "";
    public string AwsSecretAccessKey { get; set; } = "";
    public string CustomOnnxToolsPath { get; set; } = "";
    public List<string> PostExports { get; set; } = new();

    public ProjectSettings ToV2() => new()
    {
        Version = 2,
        Train = new TrainSettings
        {
            ProjectName = ProjectName,
            RunName = RunName,
            Task = Task,
            ModelVersion = ModelVersion,
            ModelSizeCode = ModelSizeCode,
            ModelName = ModelName,
            ImageSize = ImageSize,
            Epochs = Epochs,
            BatchSize = BatchSize,
            Device = Device,
            Workers = Workers
        },
        Mlflow = new MlflowSettings
        {
            Enabled = UseMlflow,
            TrackingUri = MlflowTrackingUri,
            S3EndpointUrl = MlflowS3EndpointUrl,
            AwsAccessKeyId = AwsAccessKeyId,
            AwsSecretAccessKey = AwsSecretAccessKey
        },
        Augmentation = new AugmentationSettings { SelectedProfile = AugmentationProfileName },
        Tools = new ToolsSettings { OnnxToolsPath = CustomOnnxToolsPath },
        PostExports = PostExports ?? new List<string>()
    };
}
