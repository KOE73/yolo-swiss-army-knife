using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace YoloHelperApp.Services;

/// <summary>App-level (per-user) settings: UI language and recent projects MRU.</summary>
public class AppSettings
{
    public string Language { get; set; } = "RU";
    public List<string> RecentProjects { get; set; } = new();
    public string? LastProject { get; set; }
}

/// <summary>
/// Persists <see cref="AppSettings"/> to %APPDATA%\YSAK\settings.json.
/// UI-thread only; load failures silently fall back to defaults.
/// </summary>
public class AppSettingsService
{
    public const int MaxRecent = 10;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _settingsPath;
    public AppSettings Settings { get; private set; } = new();

    public AppSettingsService(string? settingsPath = null)
    {
        _settingsPath = settingsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YSAK", "settings.json");

        try
        {
            if (File.Exists(_settingsPath))
            {
                Settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_settingsPath)) ?? new AppSettings();
            }
        }
        catch
        {
            Settings = new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            string? dir = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(Settings, JsonOptions));
        }
        catch
        {
            // Settings persistence must never crash the app.
        }
    }

    public void AddRecentProject(string ysakPath)
    {
        if (string.IsNullOrWhiteSpace(ysakPath)) return;

        string normalized = Path.GetFullPath(ysakPath);
        Settings.RecentProjects.RemoveAll(p => string.Equals(p, normalized, StringComparison.OrdinalIgnoreCase));
        Settings.RecentProjects.Insert(0, normalized);
        if (Settings.RecentProjects.Count > MaxRecent)
        {
            Settings.RecentProjects.RemoveRange(MaxRecent, Settings.RecentProjects.Count - MaxRecent);
        }
        Settings.LastProject = normalized;
        Save();
    }

    public void RemoveRecentProject(string ysakPath)
    {
        int removed = Settings.RecentProjects.RemoveAll(
            p => string.Equals(p, ysakPath, StringComparison.OrdinalIgnoreCase));
        if (removed > 0) Save();
    }

    /// <summary>Recent projects that still exist on disk (does not mutate the stored list).</summary>
    public IReadOnlyList<string> GetExistingRecentProjects() =>
        Settings.RecentProjects.Where(File.Exists).ToList();
}
