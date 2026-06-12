using SKSSL.ECS;
using YamlDotNet.Serialization;

namespace SKSSL.Tests.TestData;

public class TestEntityInheritedType : Entity
{
    [YamlMember]
    public string TestString { get; set; }
}