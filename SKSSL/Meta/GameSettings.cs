using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable UnusedMember.Global

namespace SKSSL;

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

    private static string SettingsFilePath = Path.Combine(GameDirectory.RootDirectory, "settings.yaml");

    public static GameSettings Load()
    {
        var settings = new GameSettings();
        if (!File.Exists(SettingsFilePath))
        {
            ForceCreateDefault(settings);
            return settings;
        }

        // Commence the file-loading!
        IDeserializer deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        // TODO: Make settings fields adjustable so various other projects can have more / less settings than others.
        try
        {
            var text = File.ReadAllText(SettingsFilePath);
            settings = deserializer.Deserialize<GameSettings>(text);
        }
        catch (Exception e)
        {
            Log($"Failed to load game settings: {e.Message}");
            return new GameSettings();
        }

        return settings;
    }

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