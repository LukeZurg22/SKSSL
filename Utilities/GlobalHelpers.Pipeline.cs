using Microsoft.Xna.Framework.Content;

// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global

namespace KBSL.Utilities;

public static partial class GlobalHelpers
{
    /// <summary>
    /// Returns enumerated files from a specific filepath respectful to the program.
    /// Will attempt to use the Application Context's Base Directory as its root if a directory is not provided.
    /// </summary>
    public static IEnumerable<string> GetGameFiles(string? directory = null, params string[] path_s)
    {
        string fullPath = Path.Combine(directory ?? AppContext.BaseDirectory, Path.Combine(path_s));
        return Directory.EnumerateFiles(fullPath, "*", SearchOption.AllDirectories);
    }

    /// <summary>
    /// Retrieves an asset from the content pipeline manually using a provided filepath.
    /// </summary>
    /// <param name="contentManager"></param>
    /// <param name="path"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static T GetPipelineAsset<T>(ContentManager contentManager, params string[] path)
    {
        string assetPath = Path.Combine(path);
        return contentManager.Load<T>(assetPath);
    }
}