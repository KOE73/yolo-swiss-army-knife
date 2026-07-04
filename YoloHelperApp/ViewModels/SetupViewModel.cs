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

    [ObservableProperty] private string _projectName = "YoloProject";
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
    [ObservableProperty] private string _mlflowTrackingUri = "";
    [ObservableProperty] private string _mlflowS3EndpointUrl = "";
    [ObservableProperty] private string _awsAccessKeyId = "";
    [ObservableProperty] private string _awsSecretAccessKey = "";

    [ObservableProperty] private string _consoleLog = "System ready.\n";
    [ObservableProperty] private bool _isTraining = false;

    public ICommand SaveSettingsCommand { get; }
    public ICommand LoadSettingsCommand { get; }
    public ICommand StartTrainCommand { get; }
    public ICommand StopTrainCommand { get; }

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
        _projectService.ProjectLoaded += ApplySettings;

        SaveSettingsCommand = new RelayCommand(SaveSettings);
        LoadSettingsCommand = new RelayCommand(ReloadSettings);
        StartTrainCommand = new AsyncRelayCommand(StartTrainAsync);
        StopTrainCommand = new RelayCommand(() => _yoloService.StopCurrentProcess());

        if (AugmentationProfiles.Count > 0)
        {
            SelectedAugmentationProfile = AugmentationProfiles[0];
        }
    }

    partial void OnTaskChanged(string value) => UpdateModelName();
    partial void OnModelVersionChanged(int value) => UpdateModelName();
    partial void OnModelSizeChanged(ModelSizeOption value) => UpdateModelName();

    private void UpdateModelName()
    {
        ModelName = YoloModelNamingHelper.GenerateModelName(ModelVersion, ModelSize?.Code ?? "n", Task);
    }

    /// <summary>Writes UI-editable fields back into the shared settings object (without touching other sections).</summary>
    private void UpdateCurrentSettings()
    {
        var s = _projectService.Current;

        s.Train.ProjectName = ProjectName;
        s.Train.RunName = RunName;
        s.Train.Task = Task;
        s.Train.ModelVersion = ModelVersion;
        s.Train.ModelSizeCode = ModelSize?.Code ?? "n";
        s.Train.ModelName = ModelName;
        s.Train.ImageSize = ImageSize;
        s.Train.Epochs = Epochs;
        s.Train.BatchSize = BatchSize;
        s.Train.Device = Device;
        s.Train.Workers = Workers;

        s.Mlflow.Enabled = UseMlflow;
        s.Mlflow.TrackingUri = MlflowTrackingUri;
        s.Mlflow.S3EndpointUrl = MlflowS3EndpointUrl;
        s.Mlflow.AwsAccessKeyId = AwsAccessKeyId;
        s.Mlflow.AwsSecretAccessKey = AwsSecretAccessKey;

        s.Augmentation.SelectedProfile = SelectedAugmentationProfile?.ProfileName ?? "Default (Ultralytics)";
    }

    private void SaveSettings()
    {
        if (!_projectService.IsProjectOpen)
        {
            ConsoleLog += "[ERROR] Open or create a project first!\n";
            return;
        }
        UpdateCurrentSettings();
        _projectService.Save();
        ConsoleLog += "Settings saved successfully.\n";
    }

    private void ReloadSettings()
    {
        _projectService.Reload(); // raises ProjectLoaded -> ApplySettings
    }

    private void ApplySettings(ProjectSettings s)
    {
        ProjectName = s.Train.ProjectName;
        RunName = s.Train.RunName;
        Task = s.Train.Task;
        ModelVersion = s.Train.ModelVersion;

        var match = AvailableSizes.Find(sz => sz.Code == s.Train.ModelSizeCode);
        if (match != null) ModelSize = match;

        ModelName = s.Train.ModelName;
        ImageSize = s.Train.ImageSize;
        Epochs = s.Train.Epochs;
        BatchSize = s.Train.BatchSize;
        Device = s.Train.Device;
        Workers = s.Train.Workers;

        var profileMatch = AugmentationProfiles.FirstOrDefault(p => p.ProfileName == s.Augmentation.SelectedProfile);
        SelectedAugmentationProfile = profileMatch ?? AugmentationProfiles.FirstOrDefault();

        UseMlflow = s.Mlflow.Enabled;
        MlflowTrackingUri = s.Mlflow.TrackingUri;
        MlflowS3EndpointUrl = s.Mlflow.S3EndpointUrl;
        AwsAccessKeyId = s.Mlflow.AwsAccessKeyId;
        AwsSecretAccessKey = s.Mlflow.AwsSecretAccessKey;
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
            await _yoloService.RunTrainAsync(DatasetFolder, _projectService.Current, SelectedAugmentationProfile);
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
