namespace SKSSL;

/// <summary>
/// Wrapper for a game's main content folder. Used for getting game prototype, texture, and localization directories.
/// </summary>
public record GameContentDirectory
{
    /// Name of the overall directory represented.. Used for sorting and classification.
    public readonly string DirectoryTitle;
    
    /// Directory that which game content shall be read.
    public readonly string ContentDirectory;

    #region Internal Folder Access Fields

    /// <returns>Path to localization folder, or null if not found.</returns>
    public string? LocalizationFolder => GetFolder("localization");

    /// <returns>Path to internal textures, or null if not found.</returns>
    public string? TexturesFolder => GetFolder("textures");

    /// <returns>Get folder in this content directory.</returns>
    public string? GetFolder(string folder)
    {
        string dir = Path.Combine(ContentDirectory, folder);
        return !Directory.Exists(dir) ? null : dir;
    }

    #endregion

    /// TODO: Implement load order.
    private static int loadOrderCounter = 0;

    /// 
    public int LoadOrder;

    /// Creates instance of Game Directory Wrapper.
    public GameContentDirectory(string contentDirectory)
    {
        ContentDirectory = contentDirectory;
        DirectoryTitle = Path.GetFileName(contentDirectory);
        LoadOrder = loadOrderCounter++;
    }

    /// <inheritdoc />
    public override string ToString() => ContentDirectory;
}