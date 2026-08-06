namespace SKSSL;

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable once ClassNeverInstantiated.Global
public class LoadPath
{
    public int Order { get; set; }
    public string Path { get; set; }

    public bool Enabled { get; set; } = true;

    public LoadPath()
    {
    }

    public LoadPath(string path, int order, bool enabled = true)
    {
        Path = path;
        Order = order;
        Enabled = enabled;
    }
}