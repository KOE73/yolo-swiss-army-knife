using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YoloHelperApp.Models;
using YoloHelperApp.Services;

namespace YoloHelperApp.ViewModels;

public partial class AugmentationViewModel : ViewModelBase
{
    private readonly ProjectService _projectService;

    public LocalizationService Localization => LocalizationService.Instance;

    public ObservableCollection<AugmentationProfile> Profiles { get; } = new();

    [ObservableProperty]
    private AugmentationProfile? _selectedProfile;

    // Persists edits (including rename) made in-place on the currently selected profile.
    partial void OnSelectedProfileChanged(AugmentationProfile? oldValue, AugmentationProfile? newValue)
    {
        if (oldValue != null) oldValue.PropertyChanged -= OnSelectedProfilePropertyChanged;
        if (newValue != null) newValue.PropertyChanged += OnSelectedProfilePropertyChanged;
    }

    private void OnSelectedProfilePropertyChanged(object? sender, PropertyChangedEventArgs e) => _projectService.Save();

    public AugmentationViewModel(ProjectService projectService)
    {
        _projectService = projectService;
        _projectService.ProjectLoaded += ApplySettings;

        SeedDefaults();
        SelectedProfile = Profiles[0];
    }

    private static AugmentationProfile[] CreateDefaultProfiles() => new[]
    {
        // Class defaults == ultralytics defaults, so this profile is a true no-op override
        new AugmentationProfile { ProfileName = "Default (Ultralytics)" },
        new AugmentationProfile
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
        }
    };

    private void SeedDefaults()
    {
        Profiles.Clear();
        foreach (var p in CreateDefaultProfiles()) Profiles.Add(p);
    }

    /// <summary>Populates the list from the loaded project; seeds and persists defaults for new projects.</summary>
    private void ApplySettings(ProjectSettings settings)
    {
        if (settings.Augmentation.Profiles.Count == 0)
        {
            settings.Augmentation.Profiles.AddRange(CreateDefaultProfiles());
            _projectService.Save();
        }

        Profiles.Clear();
        // Shared references: UI edits mutate the settings objects directly and are persisted on Save
        foreach (var p in settings.Augmentation.Profiles) Profiles.Add(p);

        SelectedProfile = Profiles.FirstOrDefault(p => p.ProfileName == settings.Augmentation.SelectedProfile)
                          ?? Profiles.FirstOrDefault();
    }

    [RelayCommand]
    private void AddProfile()
    {
        var newProfile = new AugmentationProfile { ProfileName = "New Profile" };
        Profiles.Add(newProfile);
        _projectService.Current.Augmentation.Profiles.Add(newProfile);
        _projectService.Save();
        SelectedProfile = newProfile;
    }

    [RelayCommand]
    private void DeleteProfile()
    {
        if (SelectedProfile != null && Profiles.Count > 1)
        {
            _projectService.Current.Augmentation.Profiles.Remove(SelectedProfile);
            _projectService.Save();
            Profiles.Remove(SelectedProfile);
            SelectedProfile = Profiles[0];
        }
    }

    public void ExportAllProfiles(string targetFolder)
    {
        if (string.IsNullOrWhiteSpace(targetFolder) || !System.IO.Directory.Exists(targetFolder)) return;

        // No naming convention: keys below are already the exact snake_case names
        // Ultralytics expects (a camelCase convention would corrupt them to hsvH etc.)
        var serializer = new YamlDotNet.Serialization.SerializerBuilder().Build();

        foreach (var profile in Profiles)
        {
            var yamlObj = new System.Collections.Generic.Dictionary<string, double>
            {
                ["hsv_h"] = profile.HsvH,
                ["hsv_s"] = profile.HsvS,
                ["hsv_v"] = profile.HsvV,
                ["degrees"] = profile.Degrees,
                ["translate"] = profile.Translate,
                ["scale"] = profile.Scale,
                ["shear"] = profile.Shear,
                ["perspective"] = profile.Perspective,
                ["flipud"] = profile.Flipud,
                ["fliplr"] = profile.Fliplr,
                ["mosaic"] = profile.Mosaic,
                ["mixup"] = profile.Mixup,
                ["copy_paste"] = profile.CopyPaste
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
