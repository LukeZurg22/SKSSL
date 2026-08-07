using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

    protected IEnumerable<string> GetFiles(string directory)
        => Directory
            .EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Where(file => Extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase));
}