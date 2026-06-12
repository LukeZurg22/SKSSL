using SKSSL.ECS;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable NotAccessedPositionalProperty.Global

namespace SKSSL.Tests.TestData;

public record TestBlankComponent : Component;
public record TestOtherComponent(int x, int y) : Component;
public record TestFieldComponent : Component
{
    public int x { get; set; }
    public int y { get; set; }
}