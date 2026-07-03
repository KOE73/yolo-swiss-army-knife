using System;

namespace YoloHelperApp.Services;

public static class YoloModelNamingHelper
{
    public static string GenerateModelName(int version, string sizeChar, string task)
    {
        string t = task?.ToLower() ?? "";
        string suffix = t switch
        {
            "obb" => "-obb",
            "pose" => "-pose",
            "segment" => "-seg",
            "classify" => "-cls",
            _ => "" // detect uses no suffix by default
        };

        // YOLO naming conventions have slightly changed across versions,
        // but typically it's yolovN or yolo11.
        string prefix = version switch
        {
            8 => "yolov8",
            9 => "yolov9",
            10 => "yolov10",
            11 => "yolo11",
            _ => $"yolo{version}" // generic fallback for v12+ or others
        };

        return $"{prefix}{sizeChar}{suffix}.pt";
    }
}
