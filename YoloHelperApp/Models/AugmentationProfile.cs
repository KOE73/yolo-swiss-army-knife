using CommunityToolkit.Mvvm.ComponentModel;

namespace YoloHelperApp.Models;

/// <summary>
/// Augmentation profile. Defaults match Ultralytics YOLO training defaults,
/// so the "Default (Ultralytics)" profile behaves exactly like plain `yolo train`.
/// ObservableObject so the profile list (bound to ProfileName) and the project
/// autosave hook (bound to PropertyChanged) both react to in-place edits.
/// </summary>
public partial class AugmentationProfile : ObservableObject
{
    [ObservableProperty] private string _profileName = "Default Augmentations";

    // Filters (0 = disabled). NOTE: not applied during training yet — reserved for
    // the future dataset pre-filtering feature.
    [ObservableProperty] private int _minWidth = 0;
    [ObservableProperty] private int _minHeight = 0;

    // Ultralytics augmentations (defaults = ultralytics defaults)
    [ObservableProperty] private double _hsvH = 0.015;
    [ObservableProperty] private double _hsvS = 0.7;
    [ObservableProperty] private double _hsvV = 0.4;

    [ObservableProperty] private double _degrees = 0.0;
    [ObservableProperty] private double _translate = 0.1;
    [ObservableProperty] private double _scale = 0.5; // (0-1 adds to 1.0)
    [ObservableProperty] private double _shear = 0.0;
    [ObservableProperty] private double _perspective = 0.0;

    [ObservableProperty] private double _flipud = 0.0;
    [ObservableProperty] private double _fliplr = 0.5;
    [ObservableProperty] private double _mosaic = 1.0;
    [ObservableProperty] private double _mixup = 0.0;
    [ObservableProperty] private double _copyPaste = 0.0;

    public AugmentationProfile Clone() => (AugmentationProfile)MemberwiseClone();
}
