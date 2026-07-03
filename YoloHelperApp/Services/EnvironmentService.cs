using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace YoloHelperApp.Services;

public class EnvironmentService
{
    public struct EnvDiagnosis
    {
        public bool PythonFound { get; set; }
        public string PythonVersion { get; set; }
        public bool PipFound { get; set; }
        public bool UltralyticsFound { get; set; }
        public string UltralyticsVersion { get; set; }
        public bool CudaAvailable { get; set; }
        public string CudaVersion { get; set; }
        public string PyTorchVersion { get; set; }
    }

    public event Action<string>? OnInstallLogReceived;

    public Task<EnvDiagnosis> DiagnoseAsync()
    {
        return Task.Run(() =>
        {
            var diagnosis = new EnvDiagnosis
            {
                PythonVersion = "Not Found",
                UltralyticsVersion = "Not Found",
                CudaVersion = "N/A",
                PyTorchVersion = "Not Found"
            };

            // Python Check
            var pythonRes = RunProcessSilently("python", "--version");
            if (pythonRes.ExitCode == 0)
            {
                diagnosis.PythonFound = true;
                diagnosis.PythonVersion = pythonRes.Output.Trim();
            }

            // Pip Check
            var pipRes = RunProcessSilently("pip", "--version");
            if (pipRes.ExitCode == 0)
            {
                diagnosis.PipFound = true;
            }

            if (diagnosis.PythonFound)
            {
                // Ultralytics Check
                var ultraRes = RunProcessSilently("python", "-c \"import ultralytics; print(ultralytics.__version__)\"");
                if (ultraRes.ExitCode == 0)
                {
                    diagnosis.UltralyticsFound = true;
                    diagnosis.UltralyticsVersion = ultraRes.Output.Trim();
                }

                // PyTorch and CUDA check
                var torchRes = RunProcessSilently("python", "-c \"import torch; print(torch.__version__); print(torch.cuda.is_available()); print(torch.version.cuda)\"");
                if (torchRes.ExitCode == 0)
                {
                    var lines = torchRes.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (lines.Length >= 1) diagnosis.PyTorchVersion = lines[0];
                    if (lines.Length >= 2) diagnosis.CudaAvailable = bool.TryParse(lines[1], out bool cuda) && cuda;
                    if (lines.Length >= 3) diagnosis.CudaVersion = lines[2];
                }
            }

            return diagnosis;
        });
    }

    public Task InstallDependenciesAsync()
    {
        return Task.Run(() =>
        {
            Log("Starting environment update...");
            Log("Command: python -m pip install -U ultralytics");
            
            var startInfo = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = "-m pip install -U ultralytics",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                process.OutputDataReceived += (s, e) => { if (e.Data != null) Log(e.Data); };
                process.ErrorDataReceived += (s, e) => { if (e.Data != null) Log($"[ERROR] {e.Data}"); };

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                process.WaitForExit();
                Log("Dependency update finished.");
            }
            else
            {
                Log("[ERROR] Failed to launch python process for installation.");
            }
        });
    }

    private (int ExitCode, string Output) RunProcessSilently(string fileName, string args)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return (-1, "");

            var outputBuilder = new StringBuilder();
            process.OutputDataReceived += (s, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
            process.BeginOutputReadLine();
            process.WaitForExit();

            return (process.ExitCode, outputBuilder.ToString());
        }
        catch
        {
            return (-1, "");
        }
    }

    private void Log(string message)
    {
        OnInstallLogReceived?.Invoke($"{DateTime.Now:HH:mm:ss} - {message}");
    }
}
