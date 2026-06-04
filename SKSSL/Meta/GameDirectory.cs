using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using static System.IO.Path;

// ReSharper disable UnusedMember.Global

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
    private readonly string _location;

    /// <returns>Directory's primary folder.</returns>
    public string GetDirectoryPath() => _location;

    /// <summary>Load priority. Lower = loaded first.</summary>
    public int LoadOrder { get; }

    public static readonly string AppDataDir =
        Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), GameManager.GameName);

    #region Internal Folder Access

    public void LoadPrototypes()
    {
    }

    public void LoadLocalization()
    {
        if (LocalizationFolder == null) return;
        Loc.Load(LocalizationFolder);
    }

    public void LoadTextures()
    {
        if (TexturesFolder == null) return;

        GetGameFiles(TexturesFolder);
    }


    /// <summary>
    /// Returns enumerated files from a specific folder path respectful to the program's executable.
    /// Will attempt to use the Application Context's Base Directory as its root if a directory is not provided.
    /// </summary>
    /// <remarks>
    /// Directory is optional, as it will use the game's base application directory instead if
    /// not provided.
    /// </remarks>
    [Pure]
    public static IEnumerable<string> GetGameFiles(string directory, params string[] path_s)
    {
        string fullPath = Combine(directory, Combine(path_s));
        return Directory.EnumerateFiles(fullPath, "*", SearchOption.AllDirectories);
    }

    /// Nullable string to state that the folder may be locally-absent. 
    public string? LocalizationFolder => GetSubFolder("localization");

    /// Nullable string to state that the folder may be locally-absent. 
    public string? TexturesFolder => GetSubFolder("textures");

    /// Nullable string to state that the folder may be locally-absent. 
    public string? PrototypesFolder => GetSubFolder("prototypes");

    /// <summary>Returns path to a subfolder if it exists, otherwise null.</summary>
    private string? GetSubFolder(string folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName))
            ArgumentException.ThrowIfNullOrWhiteSpace(folderName);

        string fullPath = Combine(_location, folderName);

        // If the directory exists, then load it from the root.
        return Directory.Exists(fullPath) ? fullPath : null;

        string supposedGameDirectorySubFolder = Combine(RootDirectory, "game", folderName);

        // Check if "game" exists first, and use its subfolders instead.
        // When all else fails, use folders in root directory.
        return Directory.Exists(supposedGameDirectorySubFolder)
            ? supposedGameDirectorySubFolder
            : Combine(RootDirectory, folderName);
    }

    #endregion

    private static int _nextLoadOrder = 0;

    /// <summary>
    /// Creates a new GameDirectory.
    /// </summary>
    public GameDirectory(string directory = "", int? explicitLoadOrder = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        // For a typical directory.
        if (string.IsNullOrEmpty(directory))
        {
            // If no directory is provided, assume that the root directory is where everything is being handled.
            _location = RootDirectory;
        }
        else
        {
            var resolvedDirectory = Combine(RootDirectory, directory);
            if (!Directory.Exists(resolvedDirectory))
            {
                Log($"Failing to add \'{directory}\' to directory list. " +
                    $"Creating empty folder at \'{RootDirectory}\'.", LOG.FILE_ERROR);
                Directory.CreateDirectory(resolvedDirectory);
            }

            _location = resolvedDirectory;
        }

        DirectoryTitle = directory;
        LoadOrder = explicitLoadOrder ?? _nextLoadOrder++;
    }

    public override string ToString() => RootDirectory;

    /// Sorting by LoadOrder.
    public int CompareTo(GameDirectory? other) => other is null ? 1 : LoadOrder.CompareTo(other.LoadOrder);
}