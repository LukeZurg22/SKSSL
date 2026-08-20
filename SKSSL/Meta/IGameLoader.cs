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

    protected static IEnumerable<string> GetFiles(string directory, params string[] extensions) => Directory
        .EnumerateFiles(directory, "*", SearchOption.AllDirectories)
        .Where(file => extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase));

    protected IEnumerable<string> GetFiles(string directory) => GetFiles(directory, Extensions);
}