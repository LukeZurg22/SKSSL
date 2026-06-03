using System;
using System.Collections.Generic;
using System.IO;
using static System.IO.Path;

// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SKSSL;

/// <summary>
/// Wrapper for a game's content folder for getting game prototype, texture, and localization directories.
/// </summary>
public sealed class GameDirectory : IComparable<GameDirectory>
{
    /// <summary>Name of the overall directory. Used for sorting and classification.</summary>
    public string DirectoryTitle { get; }

    /// <summary>Root of this current directory that contains its content.</summary>
    public static string RootDirectory { get; } = GetFullPath(Combine(AppContext.BaseDirectory, ".."));

    /// Default to "game"
    public readonly string ThisDirectory;

    /// <summary>Load priority. Lower = loaded first.</summary>
    public int LoadOrder { get; }

    public static readonly string AppDataDir =
        Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), GameManager.GameName);

    private readonly string DefaultLocaleFolder = Combine(RootDirectory, "localization");
    private readonly string DefaultPrototypesFolder = Combine(RootDirectory, "prototypes");
    private readonly string DefaultTexturesFolder = Combine(RootDirectory, "textures");


    #region Internal Folder Access

    public void LoadPrototypes()
    {
    }

    public void LoadLocalization()
    {
    }

    public IEnumerable<string> GetTextureFiles()
        => Directory.EnumerateFiles(TexturesFolder, "*", SearchOption.AllDirectories);


    /// <summary>
    /// Returns enumerated files from a specific folder path respectful to the program's executable.
    /// Will attempt to use the Application Context's Base Directory as its root if a directory is not provided.
    /// </summary>
    /// <remarks>
    /// Directory is optional, as it will use the game's base application directory instead if
    /// not provided.
    /// </remarks>
    public IEnumerable<string> GetGameFiles(string directory, params string[] path_s)
    {
        string fullPath = Combine(directory, Combine(path_s));
        return Directory.EnumerateFiles(fullPath, "*", SearchOption.AllDirectories);
    }

    public string LocalizationFolder
    {
        get
        {
            string? folder = GetFolder("localization");
            return string.IsNullOrEmpty(folder) ? DefaultLocaleFolder : folder;
        }
    }

    public string TexturesFolder
    {
        get
        {
            string? folder = GetFolder("textures");
            return string.IsNullOrEmpty(folder) ? DefaultTexturesFolder : folder;
        }
    }

    public string PrototypesFolder
    {
        get
        {
            string? locale = GetFolder("prototypes");
            return string.IsNullOrEmpty(locale) ? DefaultPrototypesFolder : locale;
        }
    }

    /// <summary>Returns path to a subfolder if it exists, otherwise null.</summary>
    private string? GetFolder(string folderName)
    {
        bool found = false;
        if (string.IsNullOrWhiteSpace(folderName))
            return null;

        string fullPath = Combine(RootDirectory, folderName);

        // If the directory exists, then load it from the root.
        if (Directory.Exists(fullPath))
            return fullPath;

        // Directory does not exist. We may be able to load a special folder anyway?
        switch (folderName)
        {
            case "localization":
                found = true;
                fullPath = Combine(RootDirectory, DefaultLocaleFolder);
                break;
            case "textures":
                found = true;
                fullPath = Combine(RootDirectory, DefaultTexturesFolder);
                break;
            case "prototypes":
                found = true;
                fullPath = Combine(RootDirectory, DefaultPrototypesFolder);
                break;
        }

        return found ? fullPath : null;
    }

    #endregion

    private static int _nextLoadOrder = 0;

    /// <summary>
    /// Creates a new GameDirectory.
    /// </summary>
    public GameDirectory(string directory = "", int? explicitLoadOrder = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        if (!string.IsNullOrEmpty(directory))
        {
            var resolvedDirectory = GetFullPath(directory);
            if (!Directory.Exists(resolvedDirectory))
            {
                resolvedDirectory = Combine(RootDirectory, "game");
                Directory.CreateDirectory(resolvedDirectory);
            }

            ThisDirectory = resolvedDirectory;
        }
        else
        {
            ThisDirectory = RootDirectory;
        }

        DirectoryTitle = new DirectoryInfo(ThisDirectory).Name;
        LoadOrder = explicitLoadOrder ?? _nextLoadOrder++;
    }

    public override string ToString() => RootDirectory;

    /// Sorting by LoadOrder.
    public int CompareTo(GameDirectory? other) => other is null ? 1 : LoadOrder.CompareTo(other.LoadOrder);
}