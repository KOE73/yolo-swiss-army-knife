using System;
using System.IO;
using System.Linq;
using YoloHelperApp.Services;

namespace YoloHelperApp.Tests;

public class AppSettingsServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _settingsPath;

    public AppSettingsServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ysak_appsettings_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _settingsPath = Path.Combine(_tempDir, "settings.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public void Defaults_WhenFileMissing()
    {
        var service = new AppSettingsService(_settingsPath);
        Assert.Equal("RU", service.Settings.Language);
        Assert.Empty(service.Settings.RecentProjects);
        Assert.Null(service.Settings.LastProject);
    }

    [Fact]
    public void Defaults_WhenFileCorrupt()
    {
        File.WriteAllText(_settingsPath, "{not json!!");
        var service = new AppSettingsService(_settingsPath);
        Assert.Equal("RU", service.Settings.Language);
    }

    [Fact]
    public void SaveLoad_RoundTrips()
    {
        var service = new AppSettingsService(_settingsPath);
        service.Settings.Language = "EN";
        service.AddRecentProject(Path.Combine(_tempDir, "a.ysak"));
        service.Save();

        var reloaded = new AppSettingsService(_settingsPath);
        Assert.Equal("EN", reloaded.Settings.Language);
        Assert.Single(reloaded.Settings.RecentProjects);
        Assert.Equal(Path.Combine(_tempDir, "a.ysak"), reloaded.Settings.LastProject);
    }

    [Fact]
    public void AddRecentProject_InsertsAtFront_AndDedupesCaseInsensitive()
    {
        var service = new AppSettingsService(_settingsPath);
        string a = Path.Combine(_tempDir, "a.ysak");
        string b = Path.Combine(_tempDir, "b.ysak");

        service.AddRecentProject(a);
        service.AddRecentProject(b);
        service.AddRecentProject(a.ToUpperInvariant()); // dedupe: moves to front

        Assert.Equal(2, service.Settings.RecentProjects.Count);
        Assert.Equal(a, service.Settings.RecentProjects[0], ignoreCase: true);
        Assert.Equal(b, service.Settings.RecentProjects[1]);
    }

    [Fact]
    public void AddRecentProject_CapsAtMaxRecent()
    {
        var service = new AppSettingsService(_settingsPath);
        for (int i = 0; i < AppSettingsService.MaxRecent + 5; i++)
        {
            service.AddRecentProject(Path.Combine(_tempDir, $"p{i}.ysak"));
        }

        Assert.Equal(AppSettingsService.MaxRecent, service.Settings.RecentProjects.Count);
        // Newest first, oldest trimmed
        Assert.EndsWith($"p{AppSettingsService.MaxRecent + 4}.ysak", service.Settings.RecentProjects[0]);
    }

    [Fact]
    public void RemoveRecentProject_Removes()
    {
        var service = new AppSettingsService(_settingsPath);
        string a = Path.Combine(_tempDir, "a.ysak");
        service.AddRecentProject(a);
        service.RemoveRecentProject(a);
        Assert.Empty(service.Settings.RecentProjects);
    }

    [Fact]
    public void GetExistingRecentProjects_FiltersMissingFiles_WithoutMutating()
    {
        var service = new AppSettingsService(_settingsPath);
        string existing = Path.Combine(_tempDir, "real.ysak");
        File.WriteAllText(existing, "version: 2");
        string missing = Path.Combine(_tempDir, "gone.ysak");

        service.AddRecentProject(missing);
        service.AddRecentProject(existing);

        var visible = service.GetExistingRecentProjects();
        Assert.Equal(new[] { existing }, visible);
        Assert.Equal(2, service.Settings.RecentProjects.Count); // stored list untouched
    }
}
