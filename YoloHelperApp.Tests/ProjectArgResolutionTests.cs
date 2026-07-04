using System;
using System.IO;
using YoloHelperApp.Services;
using YoloHelperApp.ViewModels;

namespace YoloHelperApp.Tests;

public class ProjectArgResolutionTests : IDisposable
{
    private readonly string _tempDir;

    public ProjectArgResolutionTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ysak_args_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public void Resolves_DirectYsakPath()
    {
        string ysak = Path.Combine(_tempDir, "proj.ysak");
        File.WriteAllText(ysak, "version: 2");

        Assert.True(MainWindowViewModel.TryResolveProjectArg(new[] { ysak }, out string resolved));
        Assert.Equal(ysak, resolved);
    }

    [Fact]
    public void Resolves_FolderWithSingleYsak()
    {
        string ysak = Path.Combine(_tempDir, "proj.ysak");
        File.WriteAllText(ysak, "version: 2");

        Assert.True(MainWindowViewModel.TryResolveProjectArg(new[] { _tempDir }, out string resolved));
        Assert.Equal(ysak, resolved);
    }

    [Fact]
    public void Resolves_LegacyFolderFlag()
    {
        string ysak = Path.Combine(_tempDir, ProjectService.LegacyProjectFileName);
        File.WriteAllText(ysak, "version: 2");

        Assert.True(MainWindowViewModel.TryResolveProjectArg(new[] { "--folder", _tempDir }, out string resolved));
        Assert.Equal(ysak, resolved);
        Assert.True(MainWindowViewModel.TryResolveProjectArg(new[] { "-f", _tempDir }, out _));
    }

    [Fact]
    public void Fails_ForGarbageOrEmpty()
    {
        Assert.False(MainWindowViewModel.TryResolveProjectArg(null, out _));
        Assert.False(MainWindowViewModel.TryResolveProjectArg(Array.Empty<string>(), out _));
        Assert.False(MainWindowViewModel.TryResolveProjectArg(new[] { Path.Combine(_tempDir, "missing.ysak") }, out _));
        Assert.False(MainWindowViewModel.TryResolveProjectArg(new[] { _tempDir }, out _)); // folder without .ysak
        Assert.False(MainWindowViewModel.TryResolveProjectArg(new[] { "--folder" }, out _)); // flag without value
    }

    [Fact]
    public void Fails_ForNonYsakFile()
    {
        string txt = Path.Combine(_tempDir, "readme.txt");
        File.WriteAllText(txt, "hello");
        Assert.False(MainWindowViewModel.TryResolveProjectArg(new[] { txt }, out _));
    }
}
