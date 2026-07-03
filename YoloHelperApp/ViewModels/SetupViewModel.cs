using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YoloHelperApp.Models;
using YoloHelperApp.Services;

namespace YoloHelperApp.ViewModels;

public partial class SetupViewModel : ViewModelBase
{
    private readonly ProjectService _projectService;
    private readonly YoloService _yoloService;
    private readonly AugmentationViewModel _augmentationVM;

    [ObservableProperty] private string _projectName = "ImageText2RBox";
    [ObservableProperty] private string _runName = "run1";
    [ObservableProperty] private string _task = "detect";
    [ObservableProperty] private int _modelVersion = 11;
    [ObservableProperty] private ModelSizeOption _modelSize;
    [ObservableProperty] private string _modelName = "yolo11n.pt";
    [ObservableProperty] private int _imageSize = 640;
    [ObservableProperty] private int _epochs = 100;
    [ObservableProperty] private int _batchSize = -1;
    [ObservableProperty] private int _device = 0;
    [ObservableProperty] private int _workers = 2;

    [ObservableProperty] private AugmentationProfile? _selectedAugmentationProfile;
    public System.Collections.ObjectModel.ObservableCollection<AugmentationProfile> AugmentationProfiles => _augmentationVM.Profiles;

    [ObservableProperty] private string _datasetFolder = "";

    [ObservableProperty] private bool _useMlflow = false;
    [ObservableProperty] private string _mlflowTrackingUri = "http://cis-ubuntu1.kombinat.ru:5000/";
    [ObservableProperty] private string _mlflowS3EndpointUrl = "http://cis-ubuntu1.kombinat.ru:9100";
    [ObservableProperty] private string _awsAccessKeyId = "zjJaR4SfoS2oqQVsfARv";
    [ObservableProperty] private string _awsSecretAccessKey = "IzOeYnqXByBSTzCguUjHcFW8PjMB7HFA7TosdQo1";

    [ObservableProperty] private string _consoleLog = "System ready.\n";
    [ObservableProperty] private bool _isTraining = false;

    public ICommand SaveSettingsCommand { get; }
    public ICommand LoadSettingsCommand { get; }
    public ICommand StartTrainCommand { get; }

    public class ModelSizeOption
    {
        public string Name { get; set; } = "";
        public string Code { get; set; } = "";
    }

    public System.Collections.Generic.List<string> AvailableTasks { get; } = new() 
    { 
        "detect", "pose", "obb", "segment", "classify" 
    };

    public System.Collections.Generic.List<ModelSizeOption> AvailableSizes { get; } = new()
    {
        new ModelSizeOption { Name = "Nano", Code = "n" },
        new ModelSizeOption { Name = "Small", Code = "s" },
        new ModelSizeOption { Name = "Medium", Code = "m" },
        new ModelSizeOption { Name = "Large", Code = "l" },
        new ModelSizeOption { Name = "Extra Large", Code = "x" }
    };

    public SetupViewModel(ProjectService projectService, YoloService yoloService, AugmentationViewModel augmentationVM)
    {
        _projectService = projectService;
        _yoloService = yoloService;
        _augmentationVM = augmentationVM;
        
        _modelSize = AvailableSizes[0];

        _yoloService.OnLogReceived += log => ConsoleLog += log + "\n";

        SaveSettingsCommand = new RelayCommand(SaveSettings);
        LoadSettingsCommand = new RelayCommand(LoadSettings);
        StartTrainCommand = new AsyncRelayCommand(StartTrainAsync);

        if (AugmentationProfiles.Count > 0)
        {
            SelectedAugmentationProfile = AugmentationProfiles[0];
        }
    }

    // Auto-load project settings when project folder changes (preserves UseMlflow etc.)
    partial void OnDatasetFolderChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && Directory.Exists(value))
        {
            LoadSettings();
        }
    }

    partial void OnTaskChanged(string value) => UpdateModelName();
    partial void OnModelVersionChanged(int value) => UpdateModelName();
    partial void OnModelSizeChanged(ModelSizeOption value) => UpdateModelName();

    private void UpdateModelName()
    {
        ModelName = YoloModelNamingHelper.GenerateModelName(ModelVersion, ModelSize?.Code ?? "n", Task);
    }

    public ProjectSettings GetCurrentSettings()
    {
        return new ProjectSettings
        {
            ProjectName = ProjectName,
            RunName = RunName,
            Task = Task,
            ModelVersion = ModelVersion,
            ModelSizeCode = ModelSize?.Code ?? "n",
            ModelName = ModelName,
            ImageSize = ImageSize,
            Epochs = Epochs,
            BatchSize = BatchSize,
            Device = Device,
            Workers = Workers,
            AugmentationProfileName = SelectedAugmentationProfile?.ProfileName ?? "Default (Ultralytics)",
            DatasetFolder = DatasetFolder,
            UseMlflow = UseMlflow,
            MlflowTrackingUri = MlflowTrackingUri,
            MlflowS3EndpointUrl = MlflowS3EndpointUrl,
            AwsAccessKeyId = AwsAccessKeyId,
            AwsSecretAccessKey = AwsSecretAccessKey
        };
    }

    private void SaveSettings()
    {
        if (string.IsNullOrWhiteSpace(DatasetFolder)) return;
        _projectService.SaveProject(DatasetFolder, GetCurrentSettings());
        ConsoleLog += "Settings saved successfully.\n";
    }

    private void LoadSettings()
    {
        if (string.IsNullOrWhiteSpace(DatasetFolder) || !Directory.Exists(DatasetFolder)) return;
        var s = _projectService.LoadProject(DatasetFolder);
        ProjectName = s.ProjectName;
        RunName = s.RunName;
        Task = s.Task;
        ModelVersion = s.ModelVersion;
        
        var match = AvailableSizes.Find(sz => sz.Code == s.ModelSizeCode);
        if (match != null) ModelSize = match;
        
        ModelName = s.ModelName;
        ImageSize = s.ImageSize;
        Epochs = s.Epochs;
        BatchSize = s.BatchSize;
        Device = s.Device;
        Workers = s.Workers;

        var profileMatch = AugmentationProfiles.FirstOrDefault(p => p.ProfileName == s.AugmentationProfileName);
        if (profileMatch != null) SelectedAugmentationProfile = profileMatch;

        UseMlflow = s.UseMlflow;
        MlflowTrackingUri = s.MlflowTrackingUri;
        MlflowS3EndpointUrl = s.MlflowS3EndpointUrl;
        AwsAccessKeyId = s.AwsAccessKeyId;
        AwsSecretAccessKey = s.AwsSecretAccessKey;
        ConsoleLog += "Settings loaded successfully.\n";
    }

    private async Task StartTrainAsync()
    {
        if (string.IsNullOrWhiteSpace(DatasetFolder))
        {
            ConsoleLog += "[ERROR] Choose a project/dataset directory first!\n";
            return;
        }

        IsTraining = true;
        SaveSettings();
        try
        {
            await _yoloService.RunTrainAsync(DatasetFolder, GetCurrentSettings(), SelectedAugmentationProfile);
        }
        catch (Exception ex)
        {
            ConsoleLog += $"[ERROR] Training failed: {ex.Message}\n";
        }
        finally
        {
            IsTraining = false;
        }
    }
}
