using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable UnusedMember.Global

namespace SKSSL;

// ReSharper disable once ClassNeverInstantiated.Global
public class LoadPath
{
    public string Path { get; set; }
    public int Order { get; set; }

    public LoadPath(string path, int order)
    {
        Path = path;
        Order = order;
    }
}

[JsonObject]
public class GameSettings
{
    /// Screen Width
    public uint Width { get; set; } = 1920;

    /// Screen Height
    public uint Height { get; set; } = 1080;

    /// Paths to game and mod content folders.
    public List<LoadPath> GamePaths { get; set; } = [];

    /// Language culture of the game.
    public string Language { get; set; } = "en-US";

    public static string SettingsFilePath = Path.Combine(GameDirectory.RootDirectory, "settings.yaml");

    public static void ForceCreateDefault(GameSettings settings)
    {
        if (!Directory.Exists(SettingsFilePath))
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath)!);

        ISerializer serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
        var yaml = serializer.Serialize(settings);

        File.WriteAllText(SettingsFilePath, yaml);
    }
}