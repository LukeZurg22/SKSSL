namespace SKSSL;

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable once ClassNeverInstantiated.Global
public class LoadPath
{
    public int Order { get; set; }
    public string Path { get; set; }

    public LoadPath()
    {
    }

    public LoadPath(string path, int order)
    {
        Path = path;
        Order = order;
    }
}