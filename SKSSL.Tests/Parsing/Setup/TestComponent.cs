using SKSSL.ECS;

namespace SKSSL.Tests.Tests.Parsing.Setup;

public record TestIskComponent : ISKComponent
{
    public int x { get; set; }
    public int y { get; set; }
}