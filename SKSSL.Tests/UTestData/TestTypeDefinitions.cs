using System;
using SKSSL.ECS;
using YamlDotNet.Serialization;


// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable UnusedType.Global

namespace SKSSL.Tests.TestData;

public class TestPrototypeBlank : Prototype;

public class TestPrototypeSingle : Prototype
{
    public int FirstField { get; set; }
}

public class TestPrototypeInherit : TestPrototypeSingle
{
    public int SecondField { get; set; }
}

public partial class TestPrototypeSerialize : Prototype;

public class TestPrototypeConstructor : Prototype
{
    public int FirstField;

    public TestPrototypeConstructor(TestPrototypeSingle yaml)
        : base(yaml)
    {
        FirstField = yaml.FirstField;
    }

    public Type EntityType => typeof(TestEntityInheritedType);
}

public class TestPrototypeConstructorTwo : TestPrototypeConstructor
{
    private int SecondField = 0;

    public TestPrototypeConstructorTwo(TestPrototypeInherit yaml)
        : base(yaml)
    {
        FirstField = yaml.FirstField;
        SecondField = yaml.SecondField;
    }
}

public class TestEntityInheritedType : Entity, ICloneable<TestEntityInheritedType>
{
    [YamlMember] public string TestString { get; set; }

    public override TestEntityInheritedType CopyFrom(Entity source)
    {
        base.CopyFrom(source);
        if (source is TestEntityInheritedType type) TestString = type.TestString;
        return this;
    }

    public override TestEntityInheritedType Clone() => new TestEntityInheritedType().CopyFrom(this);
}