using System;
using System.IO;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SKSSL;

/// <summary>
/// Wrapper for a game's content folder for getting game prototype, texture, and localization directories.
/// </summary>
public sealed class GameDirectory : IComparable<GameDirectory>
{
    /// <summary>Name of the overall directory. Used for sorting and classification.</summary>
    public string DirectoryTitle { get; }

    /// <summary>Root directory that contains game content.</summary>
    public string RootDirectory { get; }

    /// <summary>Load priority. Lower = loaded first.</summary>
    public int LoadOrder { get; }

    #region Internal Folder Access

    public string? LocalizationFolder => GetFolder("localization");
    public string? TexturesFolder => GetFolder("textures");
    public string? PrototypesFolder => GetFolder("prototypes");

    /// <summary>Returns path to a subfolder if it exists, otherwise null.</summary>
    public string? GetFolder(string folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName))
            return null;

        string fullPath = Path.Combine(RootDirectory, folderName);
        return Directory.Exists(fullPath) ? fullPath : null;
    }

    #endregion

    private static int _nextLoadOrder = 0;

    /// <summary>
    /// Creates a new GameDirectory.
    /// </summary>
    public GameDirectory(string directory, int? explicitLoadOrder = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        RootDirectory = Path.GetFullPath(directory); // Normalize path
        DirectoryTitle = Path.GetFileName(directory.TrimEnd('\\', '/'));

        LoadOrder = explicitLoadOrder ?? _nextLoadOrder++;
    }

    public override string ToString() => RootDirectory;

    /// Sorting by LoadOrder.
    public int CompareTo(GameDirectory? other) => other is null ? 1 : LoadOrder.CompareTo(other.LoadOrder);
}