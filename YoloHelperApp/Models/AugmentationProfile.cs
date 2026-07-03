using System;

namespace YoloHelperApp.Models;

public class AugmentationProfile
{
    public string ProfileName { get; set; } = "Default Augmentations";
    
    // Filters
    public int MinWidth { get; set; } = 500;
    public int MinHeight { get; set; } = 400;

    // Ultralytics augmentations
    public double HsvH { get; set; } = 0.015;
    public double HsvS { get; set; } = 0.7;
    public double HsvV { get; set; } = 0.4;
    
    public double Degrees { get; set; } = 15.0;
    public double Translate { get; set; } = 0.1;
    public double Scale { get; set; } = 0.5; // (0-1 adds to 1.0)
    public double Shear { get; set; } = 5.0;
    public double Perspective { get; set; } = 0.0002;
    
    public double Flipud { get; set; } = 0.0;
    public double Fliplr { get; set; } = 0.0;
    public double Mosaic { get; set; } = 0.0;
    public double Mixup { get; set; } = 0.0;
    public double CopyPaste { get; set; } = 0.0;
}
