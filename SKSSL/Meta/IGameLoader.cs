using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SKSSL;

public abstract class IGameLoader
{
    /// <summary>
    /// Custom supported extensions provided by developer implementation. OVERRIDE ME!
    /// </summary>
    public virtual string[] Extensions => [];

    public abstract void Load(string directory);

    protected IEnumerable<string> GetFiles(string directory)
        => Directory
            .EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Where(file => Extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase));
}