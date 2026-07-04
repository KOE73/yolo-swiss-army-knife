using System;
using System.IO;
using YoloHelperApp.Models;
using YoloHelperApp.Services;

namespace YoloHelperApp.Tests;

public class ProjectServiceTests : IDisposable
{
    private readonly string _tempDir;

    public ProjectServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ysak_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private string ProjectPath(string name = "myproj.ysak") => Path.Combine(_tempDir, name);

    [Fact]
    public void CreateProject_WritesDefaultV2()
    {
        var service = new ProjectService();
        var settings = service.CreateProject(ProjectPath());

        Assert.Equal(2, settings.Version);
        Assert.True(File.Exists(ProjectPath()));
        Assert.True(service.IsProjectOpen);
        Assert.Equal("myproj", service.CurrentProjectName);
        Assert.Equal(_tempDir, service.CurrentFolder);
        Assert.Equal("detect", settings.Train.Task);
        Assert.Empty(settings.Mlflow.AwsAccessKeyId); // secrets must default to empty
        Assert.Empty(settings.Mlflow.TrackingUri);
    }

    [Fact]
    public void LoadProjectFile_Throws_WhenFileMissing()
    {
        var service = new ProjectService();
        Assert.Throws<FileNotFoundException>(() => service.LoadProjectFile(ProjectPath("nope.ysak")));
        Assert.False(service.IsProjectOpen);
    }

    [Fact]
    public void ParseSettings_MigratesFlatV1File()
    {
        // Flat v1 schema as written by the old ProjectService (camelCase, no version key).
        // Note "modelVersion:" must not be mistaken for the v2 "version:" marker.
        string v1 = """
            projectName: MyProj
            runName: run7
            task: obb
            modelVersion: 11
            modelSizeCode: s
            modelName: yolo11s-obb.pt
            imageSize: 1280
            epochs: 250
            batchSize: 8
            device: 0
            workers: 4
            augmentationProfileName: Aggressive Augmentation
            useMlflow: true
            mlflowTrackingUri: http://host:5000/
            customOnnxToolsPath: D:\tools
            postExports:
            - deploy.ps1
            - copy_models.bat
            """;

        var service = new ProjectService();
        var settings = service.ParseSettings(v1);

        Assert.NotNull(settings);
        Assert.Equal(2, settings!.Version);
        Assert.Equal("MyProj", settings.Train.ProjectName);
        Assert.Equal("run7", settings.Train.RunName);
        Assert.Equal("obb", settings.Train.Task);
        Assert.Equal(1280, settings.Train.ImageSize);
        Assert.Equal(250, settings.Train.Epochs);
        Assert.True(settings.Mlflow.Enabled);
        Assert.Equal("http://host:5000/", settings.Mlflow.TrackingUri);
        Assert.Equal("Aggressive Augmentation", settings.Augmentation.SelectedProfile);
        Assert.Equal(@"D:\tools", settings.Tools.OnnxToolsPath);
        Assert.Equal(new[] { "deploy.ps1", "copy_models.bat" }, settings.PostExports);
    }

    [Fact]
    public void SaveLoad_RoundTripsV2_IncludingProfilesAndPostExports()
    {
        var service = new ProjectService();
        var settings = service.CreateProject(ProjectPath());
        settings.Train.Epochs = 42;
        settings.PostExports.Add("deploy.ps1");
        settings.Augmentation.Profiles.Add(new AugmentationProfile { ProfileName = "Custom", Degrees = 33.0 });
        settings.Export.Profiles.Add(new ExportProfile
        {
            Name = "TensorRT",
            Format = "engine",
            Opsets = { 13 },
            Precisions = { "FP16" }
        });

        service.Save();
        var loaded = new ProjectService().LoadProjectFile(ProjectPath());

        Assert.Equal(2, loaded.Version);
        Assert.Equal(42, loaded.Train.Epochs);
        Assert.Equal(new[] { "deploy.ps1" }, loaded.PostExports);

        var aug = Assert.Single(loaded.Augmentation.Profiles);
        Assert.Equal("Custom", aug.ProfileName);
        Assert.Equal(33.0, aug.Degrees);

        var exp = Assert.Single(loaded.Export.Profiles);
        Assert.Equal("TensorRT", exp.Name);
        Assert.Equal("engine", exp.Format);
        Assert.Contains(13, exp.Opsets);
        Assert.Contains("FP16", exp.Precisions);
    }

    [Fact]
    public void ParseSettings_ReturnsNull_ForEmptyOrGarbage()
    {
        var service = new ProjectService();
        Assert.Null(service.ParseSettings(""));
        Assert.Null(service.ParseSettings("   "));
        Assert.Null(service.ParseSettings("{{{not yaml: ["));
    }

    [Fact]
    public void LoadProjectFile_SurvivesCorruptedFile()
    {
        File.WriteAllText(ProjectPath(), "{{{broken");
        var service = new ProjectService();
        var settings = service.LoadProjectFile(ProjectPath());

        Assert.NotNull(settings);
        Assert.Equal(2, settings.Version); // falls back to defaults instead of crashing
        Assert.True(service.IsProjectOpen);
    }

    [Fact]
    public void ResolveProjectFileInFolder_ReturnsNull_ForEmptyFolder()
    {
        Assert.Null(ProjectService.ResolveProjectFileInFolder(_tempDir));
        Assert.Null(ProjectService.ResolveProjectFileInFolder(Path.Combine(_tempDir, "missing_subdir")));
        Assert.Null(ProjectService.ResolveProjectFileInFolder(""));
    }

    [Fact]
    public void ResolveProjectFileInFolder_FindsSingleYsak()
    {
        File.WriteAllText(ProjectPath("foo.ysak"), "version: 2");
        Assert.Equal(ProjectPath("foo.ysak"), ProjectService.ResolveProjectFileInFolder(_tempDir));
    }

    [Fact]
    public void ResolveProjectFileInFolder_PrefersLegacyProjectYsak()
    {
        File.WriteAllText(ProjectPath("foo.ysak"), "version: 2");
        File.WriteAllText(ProjectPath(ProjectService.LegacyProjectFileName), "version: 2");
        Assert.Equal(
            ProjectPath(ProjectService.LegacyProjectFileName),
            ProjectService.ResolveProjectFileInFolder(_tempDir));
    }

    [Fact]
    public void ResolveProjectFileInFolder_ReturnsNull_WhenAmbiguous()
    {
        File.WriteAllText(ProjectPath("foo.ysak"), "version: 2");
        File.WriteAllText(ProjectPath("bar.ysak"), "version: 2");
        Assert.Null(ProjectService.ResolveProjectFileInFolder(_tempDir));
    }
}
