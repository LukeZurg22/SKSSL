namespace SKSSL;

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable once ClassNeverInstantiated.Global
public class LoadPath
{
    public string Path { get; set; }
    public int Order { get; set; }

    public LoadPath(string path, int order)
    {
        Path = path;
        Order = order;
    }
}