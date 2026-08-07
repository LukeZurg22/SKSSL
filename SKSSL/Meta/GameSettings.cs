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
    /// Screen Width. Defaults to monitor if -1.
    public int Width { get; set; } = -1;

    /// Screen Height. Defaults to monitor if -1.
    public int Height { get; set; } = -1;

    public bool IsBorderless { get; set; } = false;
    public bool IsFullScreen { get; set; } = false;

    /// Default SKSSL in-built console complete with custom console command support.
    /// <remarks>Put that Source Generator to good use! -Z</remarks>
    public bool SKSSLConsoleEnabled { get; set; } = true;

    /// Language culture of the game.
    public string Language { get; set; } = "en-US";

    private static string SettingsFilePath = Path.Combine(GameDirectory.RootDirectory, "settings.yaml");
    private static string LoadOrderFilePath = Path.Combine(GameDirectory.RootDirectory, "load_order.yaml");

    public static (GameSettings, List<LoadPath>) Load()
    {
        var settings = new GameSettings();
        List<LoadPath> loadPaths = [new("game", 1)];

        // Ensure they exist, one way or another.
        if (!File.Exists(SettingsFilePath)) ForceCreateDefault<GameSettings>(settings, SettingsFilePath);
        if (!File.Exists(LoadOrderFilePath)) ForceCreateDefault<List<LoadPath>>(loadPaths, LoadOrderFilePath);

        // Commence the file-loading!
        IDeserializer deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        // TODO: Make settings fields adjustable so various other projects can have more / less settings than others.

        //@formatter:off
        // Try Loading Settings
        try { settings = deserializer.Deserialize<GameSettings>(File.ReadAllText(SettingsFilePath)); }
        catch (Exception e) { Log($"Failed to load game settings: {e.InnerException?.Message}.", LOG.SYSTEM_ERROR); }
        // Try loading Load Order Paths
        try { loadPaths = deserializer.Deserialize<List<LoadPath>>(File.ReadAllText(LoadOrderFilePath)); }
        catch (Exception e) { Log($"Failed to load game paths: {e.InnerException?.Message}.", LOG.SYSTEM_ERROR); }
        //@formatter:on

        return (settings, loadPaths);
    }

    public static void ForceCreateDefault<T>(object data, string defaultPath)
    {
        if (!Directory.Exists(defaultPath)) Directory.CreateDirectory(Path.GetDirectoryName(defaultPath)!);

        ISerializer serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
        var yaml = serializer.Serialize((T)data);

        File.WriteAllText(defaultPath, yaml);
    }
}