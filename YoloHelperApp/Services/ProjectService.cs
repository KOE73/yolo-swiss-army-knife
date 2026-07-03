using System;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YoloHelperApp.Models;

namespace YoloHelperApp.Services;

public class ProjectService
{
    private readonly ISerializer _serializer;
    private readonly IDeserializer _deserializer;

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

    public void SaveProject(string folderPath, ProjectSettings settings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        ArgumentNullException.ThrowIfNull(settings);

        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        string filePath = Path.Combine(folderPath, "project.ysak");
        string yamlContent = _serializer.Serialize(settings);
        File.WriteAllText(filePath, yamlContent);
    }

    public ProjectSettings LoadProject(string folderPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

        string filePath = Path.Combine(folderPath, "project.ysak");
        if (!File.Exists(filePath))
        {
            var defaultSettings = new ProjectSettings
            {
                DatasetFolder = folderPath
            };
            SaveProject(folderPath, defaultSettings);
            return defaultSettings;
        }

        string yamlContent = File.ReadAllText(filePath);
        return _deserializer.Deserialize<ProjectSettings>(yamlContent);
    }
}
