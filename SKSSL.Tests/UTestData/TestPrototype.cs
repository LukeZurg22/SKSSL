using System;
using SKSSL.ECS;


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