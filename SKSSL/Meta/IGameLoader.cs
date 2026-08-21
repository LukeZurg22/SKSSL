using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// ReSharper disable MemberCanBePrivate.Global

namespace SKSSL;

/// <summary>
/// Dedicated loader class to interact with game files. 
/// </summary>
/// <param name="Extensions">
/// Custom supported extensions provided by developer implementation, which
/// can be overriden with inherited primary constructor passthrough..
/// </param>
public abstract class IGameLoader(params string[] Extensions)
{
    public abstract void Load(string directory);

    protected static IEnumerable<string> GetFiles(string directory, SearchOption option, params string[] extensions)
        => Directory
            .EnumerateFiles(directory, "*", option)
            .Where(file => extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// Retrieve all files stored in directory.
    /// </summary>
    /// <param name="directory">Directory to search.</param>
    /// <param name="option">Search option.</param>
    /// <returns>Files matching extensions stored in Game Loader.</returns>
    protected IEnumerable<string> GetFiles(string directory, SearchOption option = SearchOption.AllDirectories)
        => GetFiles(directory, option, Extensions);
}