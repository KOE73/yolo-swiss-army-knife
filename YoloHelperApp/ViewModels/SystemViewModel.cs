using System;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YoloHelperApp.Services;

namespace YoloHelperApp.ViewModels;

public partial class SystemViewModel : ViewModelBase
{
    private readonly EnvironmentService _envService;

    public LocalizationService Localization => LocalizationService.Instance;

    [ObservableProperty] private bool _pythonFound = false;
    [ObservableProperty] private string _pythonVersion = "Scanning...";
    [ObservableProperty] private bool _pipFound = false;
    [ObservableProperty] private bool _ultralyticsFound = false;
    [ObservableProperty] private string _ultralyticsVersion = "Scanning...";
    [ObservableProperty] private bool _cudaAvailable = false;
    [ObservableProperty] private string _cudaVersion = "N/A";
    [ObservableProperty] private string _pyTorchVersion = "Scanning...";

    [ObservableProperty] private string _installLog = "";
    [ObservableProperty] private bool _isInstalling = false;
    [ObservableProperty] private bool _isLoading = false;

    [ObservableProperty] private string _fileAssociationStatus = "";
    public bool CanRegisterFileAssociation => OperatingSystem.IsWindows() && WindowsShellService.GetExecutablePath() != null;

    public ICommand DiagnoseCommand { get; }
    public ICommand InstallCommand { get; }
    public ICommand RegisterFileAssociationCommand { get; }

    public SystemViewModel(EnvironmentService envService)
    {
        _envService = envService;
        _envService.OnInstallLogReceived += log => InstallLog += log + "\n";

        DiagnoseCommand = new AsyncRelayCommand(DiagnoseAsync);
        InstallCommand = new AsyncRelayCommand(InstallAsync);
        RegisterFileAssociationCommand = new RelayCommand(RegisterFileAssociation);

        RefreshFileAssociationStatus();

        // Auto-diagnose on startup
        _ = DiagnoseAsync();
    }

    private void RegisterFileAssociation()
    {
        if (WindowsShellService.RegisterFileAssociation())
        {
            InstallLog += "*.ysak file association registered for current user.\n";
        }
        else
        {
            InstallLog += "[ERROR] Cannot register *.ysak association on this platform/launch mode.\n";
        }
        RefreshFileAssociationStatus();
    }

    private void RefreshFileAssociationStatus()
    {
        if (!OperatingSystem.IsWindows())
        {
            FileAssociationStatus = "Not available on this OS";
            return;
        }

        string? registered = WindowsShellService.GetRegisteredExecutable();
        if (registered == null)
        {
            FileAssociationStatus = "*.ysak is not associated";
        }
        else if (WindowsShellService.IsFileAssociationRegistered())
        {
            FileAssociationStatus = $"Registered: {registered}";
        }
        else
        {
            FileAssociationStatus = $"Registered to another exe: {registered}";
        }
    }

    public async Task DiagnoseAsync()
    {
        IsLoading = true;
        InstallLog += "Starting environment diagnostics...\n";
        var diag = await _envService.DiagnoseAsync();
        PythonFound = diag.PythonFound;
        PythonVersion = diag.PythonVersion;
        PipFound = diag.PipFound;
        UltralyticsFound = diag.UltralyticsFound;
        UltralyticsVersion = diag.UltralyticsVersion;
        CudaAvailable = diag.CudaAvailable;
        CudaVersion = diag.CudaVersion;
        PyTorchVersion = diag.PyTorchVersion;
        InstallLog += "Diagnostics completed.\n";
        IsLoading = false;
    }

    private async Task InstallAsync()
    {
        IsInstalling = true;
        try
        {
            await _envService.InstallDependenciesAsync();
            await DiagnoseAsync();
        }
        catch (Exception ex)
        {
            InstallLog += $"[ERROR] Installation failed: {ex.Message}\n";
        }
        finally
        {
            IsInstalling = false;
        }
    }
}
