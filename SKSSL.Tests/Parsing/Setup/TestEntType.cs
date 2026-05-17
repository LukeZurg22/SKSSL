using SKSSL.ECS;

namespace SKSSL.Tests.Tests.Parsing.Setup;

public record TestEntType : SKEntity
{
    public TestEntType(int id, EntityTemplate template) : base(id, template)
    {
        
    }
}