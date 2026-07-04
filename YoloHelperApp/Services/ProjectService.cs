using System;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YoloHelperApp.Models;

namespace YoloHelperApp.Services;

/// <summary>
/// Single owner of the loaded *.ysak project file. All ViewModels mutate <see cref="Current"/>
/// sections and call <see cref="Save"/>, so concurrent writers cannot wipe each other's data.
/// A project is a .ysak file; the project root is the directory containing it.
/// </summary>
public class ProjectService
{
    public const string LegacyProjectFileName = "project.ysak";
    public const string ProjectExtension = ".ysak";

    private readonly ISerializer _serializer;
    private readonly IDeserializer _deserializer;

    public ProjectSettings Current { get; private set; } = new();

    /// <summary>Full path to the loaded .ysak file; empty when no project is open.</summary>
    public string CurrentProjectFile { get; private set; } = "";

    /// <summary>Project root: directory of the loaded .ysak file.</summary>
    public string CurrentFolder =>
        string.IsNullOrEmpty(CurrentProjectFile) ? "" : Path.GetDirectoryName(CurrentProjectFile) ?? "";

    /// <summary>Display name: .ysak file name without extension.</summary>
    public string CurrentProjectName =>
        string.IsNullOrEmpty(CurrentProjectFile) ? "" : Path.GetFileNameWithoutExtension(CurrentProjectFile);

    public bool IsProjectOpen => CurrentProjectFile.Length > 0;

    /// <summary>Raised after a project file has been loaded (or created).</summary>
    public event Action<ProjectSettings>? ProjectLoaded;

    public ProjectService()
    {
        _serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    public void Save()
    {
        if (!IsProjectOpen) return;
        WriteToFile(CurrentProjectFile, Current);
    }

    /// <summary>Loads an existing .ysak file. Corrupt content falls back to defaults.</summary>
    public ProjectSettings LoadProjectFile(string ysakFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ysakFilePath);
        if (!File.Exists(ysakFilePath))
            throw new FileNotFoundException("Project file not found.", ysakFilePath);

        ProjectSettings settings = ParseSettings(File.ReadAllText(ysakFilePath)) ?? new ProjectSettings();

        Current = settings;
        CurrentProjectFile = Path.GetFullPath(ysakFilePath);
        ProjectLoaded?.Invoke(settings);
        return settings;
    }

    /// <summary>Creates a new .ysak file with default settings and opens it.</summary>
    public ProjectSettings CreateProject(string ysakFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ysakFilePath);
        WriteToFile(ysakFilePath, new ProjectSettings());
        return LoadProjectFile(ysakFilePath);
    }

    /// <summary>Re-reads the currently open project file (raises ProjectLoaded).</summary>
    public void Reload()
    {
        if (!IsProjectOpen) return;
        LoadProjectFile(CurrentProjectFile);
    }

    /// <summary>
    /// Folder compatibility: returns the project file inside <paramref name="folderPath"/> —
    /// legacy project.ysak wins, otherwise a single *.ysak; null when none or ambiguous.
    /// </summary>
    public static string? ResolveProjectFileInFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath)) return null;

        string[] candidates = Directory.GetFiles(folderPath, "*" + ProjectExtension);
        foreach (string candidate in candidates)
        {
            if (string.Equals(Path.GetFileName(candidate), LegacyProjectFileName, StringComparison.OrdinalIgnoreCase))
                return candidate;
        }
        return candidates.Length == 1 ? candidates[0] : null;
    }

    private void WriteToFile(string filePath, ProjectSettings settings)
    {
        string? dir = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(filePath, _serializer.Serialize(settings));
    }

    /// <summary>Parses v2 content, migrating flat v1 files transparently. Returns null for unreadable content.</summary>
    public ProjectSettings? ParseSettings(string yamlContent)
    {
        if (string.IsNullOrWhiteSpace(yamlContent)) return null;

        try
        {
            // v2 files carry an explicit top-level version marker.
            // (A plain Contains check would false-positive on v1's "modelVersion:".)
            bool isV2 = System.Text.RegularExpressions.Regex.IsMatch(
                yamlContent, @"^version:\s*\d+", System.Text.RegularExpressions.RegexOptions.Multiline);

            if (isV2)
            {
                return _deserializer.Deserialize<ProjectSettings>(yamlContent);
            }

            var legacy = _deserializer.Deserialize<LegacyProjectSettingsV1>(yamlContent);
            return legacy?.ToV2();
        }
        catch
        {
            return null;
        }
    }
}
