using CommunityToolkit.Mvvm.ComponentModel;
using YoloHelperApp.Services;

namespace YoloHelperApp.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ProjectService _projectService;
    private readonly DatasetService _datasetService;
    private readonly YoloService _yoloService;
    private readonly EnvironmentService _environmentService;
    private readonly InferenceService _inferenceService;

    // Sub-ViewModels
    public SetupViewModel SetupVM { get; }
    public DatasetViewModel DatasetVM { get; }
    public SystemViewModel SystemVM { get; }
    public AugmentationViewModel AugmentationVM { get; }
    
    // New unified Analyze View Model
    public AnalyzeViewModel AnalyzeVM { get; }

    public LocalizationService Localization => LocalizationService.Instance;

    [ObservableProperty] private string _currentProjectFolder = "";

    public MainWindowViewModel(string[]? args = null)
    {
        // Initialize services
        _projectService = new ProjectService();
        _datasetService = new DatasetService();
        _yoloService = new YoloService();
        _environmentService = new EnvironmentService();
        _inferenceService = new InferenceService();

        // Initialize child ViewModels
        AugmentationVM = new AugmentationViewModel();
        SetupVM = new SetupViewModel(_projectService, _yoloService, AugmentationVM);
        DatasetVM = new DatasetViewModel(_datasetService);
        SystemVM = new SystemViewModel(_environmentService);
        
        AnalyzeVM = new AnalyzeViewModel(_yoloService, _inferenceService, _projectService);

        // Wire: after reshuffle -> re-scan images in inference (AnalyzeVM)
        DatasetVM.ReshuffleCompleted += () => _ = AnalyzeVM.InferenceVM.ScanImagesAsync();

        // Parse arguments for default project folder
        string defaultFolder = System.IO.Directory.Exists(@"C:\NN\YKnife") ? @"C:\NN\YKnife" : "";
        if (args != null && args.Length > 0)
        {
            // Simple check: if first arg is a valid directory, use it
            if (System.IO.Directory.Exists(args[0]))
            {
                defaultFolder = args[0];
            }
            else
            {
                // check named parameters like --folder <path>
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (args[i] == "--folder" || args[i] == "-f")
                    {
                        defaultFolder = args[i + 1];
                        break;
                    }
                }
            }
        }

        // Set default project path
        CurrentProjectFolder = defaultFolder;
    }

    partial void OnCurrentProjectFolderChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;

        SetupVM.DatasetFolder = value; // Sets DatasetFolder
        DatasetVM.DatasetFolder = value;
        AnalyzeVM.ProjectFolder = value;

        // Auto scan directories on folder change asynchronously
        _ = DatasetVM.UpdateStatsAsync();
    }
}
