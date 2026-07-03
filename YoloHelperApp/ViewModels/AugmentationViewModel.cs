using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YoloHelperApp.Models;
using YoloHelperApp.Services;

namespace YoloHelperApp.ViewModels;

public partial class AugmentationViewModel : ViewModelBase
{
    public LocalizationService Localization => LocalizationService.Instance;

    public ObservableCollection<AugmentationProfile> Profiles { get; } = new();

    [ObservableProperty]
    private AugmentationProfile? _selectedProfile;

    public AugmentationViewModel()
    {
        // Add default profiles
        Profiles.Add(new AugmentationProfile { ProfileName = "Default (Ultralytics)" });
        Profiles.Add(new AugmentationProfile 
        { 
            ProfileName = "Aggressive Augmentation",
            HsvH = 0.05,
            HsvS = 0.9,
            HsvV = 0.6,
            Degrees = 45.0,
            Translate = 0.2,
            Scale = 0.9,
            Mosaic = 1.0,
            Mixup = 0.2
        });

        SelectedProfile = Profiles[0];
    }

    [RelayCommand]
    private void AddProfile()
    {
        var newProfile = new AugmentationProfile { ProfileName = "New Profile" };
        Profiles.Add(newProfile);
        SelectedProfile = newProfile;
    }

    [RelayCommand]
    private void DeleteProfile()
    {
        if (SelectedProfile != null && Profiles.Count > 1)
        {
            Profiles.Remove(SelectedProfile);
            SelectedProfile = Profiles[0];
        }
    }

    public void ExportAllProfiles(string targetFolder)
    {
        if (string.IsNullOrWhiteSpace(targetFolder) || !System.IO.Directory.Exists(targetFolder)) return;

        var serializer = new YamlDotNet.Serialization.SerializerBuilder()
            .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
            .Build();

        foreach (var profile in Profiles)
        {
            var yamlObj = new
            {
                hsv_h = profile.HsvH,
                hsv_s = profile.HsvS,
                hsv_v = profile.HsvV,
                degrees = profile.Degrees,
                translate = profile.Translate,
                scale = profile.Scale,
                shear = profile.Shear,
                perspective = profile.Perspective,
                flipud = profile.Flipud,
                fliplr = profile.Fliplr,
                mosaic = profile.Mosaic,
                mixup = profile.Mixup,
                copy_paste = profile.CopyPaste
            };

            string yamlContent = serializer.Serialize(yamlObj);
            
            // Clean filename
            string safeName = string.Join("_", profile.ProfileName.Split(System.IO.Path.GetInvalidFileNameChars()));
            safeName = safeName.Replace(" ", "_").ToLowerInvariant();
            
            string yamlPath = System.IO.Path.Combine(targetFolder, $"{safeName}.yaml");
            System.IO.File.WriteAllText(yamlPath, yamlContent);
        }
    }
}
