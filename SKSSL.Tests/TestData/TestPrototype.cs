using System;
using SKSSL.ECS;


// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable UnusedType.Global

namespace SKSSL.Tests.TestData;

public record TestPrototypeBlank : Prototype;

public record TestPrototypeSingle : Prototype
{
    public int FirstField { get; set; }
}

public record TestPrototypeInherit : TestPrototypeSingle
{
    public int SecondField { get; set; }
}

public partial record TestPrototypeSerialize : Prototype;

public record TestPrototypeConstructor : Prototype
{
    public int FirstField;

    public TestPrototypeConstructor(TestPrototypeSingle yaml)
        : base(yaml)
    {
        FirstField = yaml.FirstField;
    }

    public Type EntityType => typeof(TestEntityInheritedType);
}

public record TestPrototypeConstructorTwo : TestPrototypeConstructor
{
    private int SecondField = 0;

    public TestPrototypeConstructorTwo(TestPrototypeInherit yaml)
        : base(yaml)
    {
        FirstField = yaml.FirstField;
        SecondField = yaml.SecondField;
    }
}