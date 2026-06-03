namespace SKSSL;

public class LoadPath
{
    public string Path { get; set; }
    public int Order { get; set; }

    public LoadPath()
    {
    }

    public LoadPath(string path, int order)
    {
        Path = path;
        Order = order;
    }
}