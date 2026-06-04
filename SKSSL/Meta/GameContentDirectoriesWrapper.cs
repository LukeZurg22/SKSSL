using System.Collections;
using System.Collections.Generic;

// ReSharper disable UnusedMember.Global

namespace SKSSL;

public class GameContentDirectories : IEnumerable<GameDirectory>
{
    private readonly List<GameDirectory> Directories = [];

    public int Count => Directories.Count;
    
    /// <summary>
    /// Sort internal directories by load order.
    /// </summary>
    public void Sort()
        => Directories.Sort((a, b) => a.LoadOrder.CompareTo(b.LoadOrder));


    public void Add(string path = "", int? order = null) => Directories.Add(new GameDirectory(path, order));

    public IEnumerator<GameDirectory> GetEnumerator() => ((IEnumerable<GameDirectory>)Directories).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => Directories.GetEnumerator();
}