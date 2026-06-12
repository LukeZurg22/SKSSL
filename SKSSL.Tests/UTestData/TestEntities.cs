using SKSSL.ECS;

namespace SKSSL.Tests.TestData;

public record TestEntityInheritedType : Entity
{
    public string TestString { get; set; }
}