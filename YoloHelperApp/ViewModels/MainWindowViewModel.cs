using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YoloHelperApp.Services;

namespace YoloHelperApp.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private const int SystemDoctorTabIndex = 4;

    private readonly ProjectService _projectService;
    private readonly DatasetService _datasetService;
    private readonly YoloService _yoloService;
    private readonly EnvironmentService _environmentService;
    private readonly InferenceService _inferenceService;

    public AppSettingsService AppSettings { get; }

    // Sub-ViewModels
    public SetupViewModel SetupVM { get; }
    public DatasetViewModel DatasetVM { get; }
    public SystemViewModel SystemVM { get; }
    public AugmentationViewModel AugmentationVM { get; }

    // New unified Analyze View Model
    public AnalyzeViewModel AnalyzeVM { get; }

    public LocalizationService Localization => LocalizationService.Instance;

    [ObservableProperty] private bool _isProjectOpen;
    [ObservableProperty] private string _projectName = "";
    [ObservableProperty] private string _projectFilePath = "";
    [ObservableProperty] private string _windowTitle = "YOLO Swiss Army Knife";
    [ObservableProperty] private int _selectedTabIndex = SystemDoctorTabIndex;

    public ObservableCollection<string> RecentProjects { get; } = new();

    public MainWindowViewModel(string[]? args = null)
    {
        // App-level settings (language + MRU) must exist before any UI strings resolve
        AppSettings = new AppSettingsService();
        LocalizationService.Instance.Initialize(AppSettings);

        // Initialize services
        _projectService = new ProjectService();
        _datasetService = new DatasetService();
        _yoloService = new YoloService();
        _environmentService = new EnvironmentService();
        _inferenceService = new InferenceService();

        // Initialize child ViewModels (AugmentationVM first: it must repopulate
        // profiles before SetupVM resolves the selected profile on ProjectLoaded)
        AugmentationVM = new AugmentationViewModel(_projectService);
        SetupVM = new SetupViewModel(_projectService, _yoloService, AugmentationVM);
        DatasetVM = new DatasetViewModel(_datasetService);
        SystemVM = new SystemViewModel(_environmentService);

        AnalyzeVM = new AnalyzeViewModel(_yoloService, _inferenceService, _projectService);

        // Wire: after reshuffle -> re-scan images in inference (AnalyzeVM)
        DatasetVM.ReshuffleCompleted += () => _ = AnalyzeVM.InferenceVM.ScanImagesAsync();

        RefreshRecentProjects();

        if (TryResolveProjectArg(args, out string ysakPath))
        {
            OpenProject(ysakPath);
        }
    }

    /// <summary>Terminates child yolo processes on application exit.</summary>
    public void Shutdown() => _yoloService.StopCurrentProcess();

    /// <summary>
    /// Resolves CLI args to a project file: a .ysak path (Explorer double-click),
    /// a folder containing one, or legacy --folder/-f.
    /// </summary>
    public static bool TryResolveProjectArg(string[]? args, out string ysakPath)
    {
        ysakPath = "";
        if (args == null || args.Length == 0) return false;

        string? candidate = null;
        if (args[0] is { Length: > 0 } first && !first.StartsWith('-'))
        {
            candidate = first;
        }
        else
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--folder" || args[i] == "-f")
                {
                    candidate = args[i + 1];
                    break;
                }
            }
        }
        if (string.IsNullOrWhiteSpace(candidate)) return false;

        if (File.Exists(candidate) &&
            Path.GetExtension(candidate).Equals(ProjectService.ProjectExtension, System.StringComparison.OrdinalIgnoreCase))
        {
            ysakPath = candidate;
            return true;
        }

        if (Directory.Exists(candidate))
        {
            string? resolved = ProjectService.ResolveProjectFileInFolder(candidate);
            if (resolved != null)
            {
                ysakPath = resolved;
                return true;
            }
        }
        return false;
    }

    public void OpenProject(string ysakPath)
    {
        // Subscribed ViewModels (Setup, Augmentation, Export) apply settings on ProjectLoaded
        _projectService.LoadProjectFile(ysakPath);
        OnProjectOpened();
    }

    public void CreateProject(string ysakPath)
    {
        _projectService.CreateProject(ysakPath);
        OnProjectOpened();
    }

    [RelayCommand]
    private void OpenRecentProject(string path)
    {
        if (!File.Exists(path))
        {
            AppSettings.RemoveRecentProject(path);
            RefreshRecentProjects();
            return;
        }
        OpenProject(path);
    }

    private void OnProjectOpened()
    {
        string projectFile = _projectService.CurrentProjectFile;
        AppSettings.AddRecentProject(projectFile);
        WindowsShellService.NotifyProjectOpened(projectFile);
        RefreshRecentProjects();

        IsProjectOpen = true;
        ProjectName = _projectService.CurrentProjectName;
        ProjectFilePath = projectFile;
        WindowTitle = $"YSAK — {ProjectName}";

        string folder = _projectService.CurrentFolder;
        SetupVM.DatasetFolder = folder;
        DatasetVM.DatasetFolder = folder;
        AnalyzeVM.ProjectFolder = folder;

        // Auto scan dataset asynchronously
        _ = DatasetVM.UpdateStatsAsync();

        if (SelectedTabIndex == SystemDoctorTabIndex) SelectedTabIndex = 0;
    }

    private void RefreshRecentProjects()
    {
        RecentProjects.Clear();
        foreach (string path in AppSettings.GetExistingRecentProjects())
        {
            RecentProjects.Add(path);
        }
    }
}
