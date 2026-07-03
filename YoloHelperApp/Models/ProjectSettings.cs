using System;

namespace YoloHelperApp.Models;

public class ProjectSettings
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
    public int Device { get; set; } = 0; // GPU ID or -1 for CPU
    public int Workers { get; set; } = 2;

    // Dataset settings
    public string DatasetFolder { get; set; } = "";
    public string TrainImagesFolder { get; set; } = "images/train";
    public string ValImagesFolder { get; set; } = "images/val";
    public string TrainLabelsFolder { get; set; } = "labels/train";
    public string ValLabelsFolder { get; set; } = "labels/val";
    public string SplitRatio { get; set; } = "80/20"; // e.g. 80/20
    public bool UseVirtualSplit { get; set; } = true; // true = train.txt/val.txt, false = physical move

    // Data Augmentation
    public string AugmentationProfileName { get; set; } = "Default (Ultralytics)";
    public double Mosaic { get; set; } = 1.0;
    public double Degrees { get; set; } = 0.0;
    public double Translate { get; set; } = 0.1;
    public double Scale { get; set; } = 0.5;
    public double Flipud { get; set; } = 0.0;
    public double Fliplr { get; set; } = 0.5;

    // MLFlow Settings
    public bool UseMlflow { get; set; } = false;
    public string MlflowTrackingUri { get; set; } = "";
    public string MlflowS3EndpointUrl { get; set; } = "";
    public string AwsAccessKeyId { get; set; } = "";
    public string AwsSecretAccessKey { get; set; } = "";

    // Tools
    public string CustomOnnxToolsPath { get; set; } = "";

    // Post Export actions
    public System.Collections.Generic.List<string> PostExports { get; set; } = new();
}
