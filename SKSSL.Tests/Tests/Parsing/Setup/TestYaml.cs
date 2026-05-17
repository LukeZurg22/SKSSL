using System;
using System.Collections.Generic;
using SKSSL.ECS;
using SKSSL.YAML;

namespace SKSSL.Tests.Tests.Parsing.Setup;

public record TestTaml : EntityYaml
{
    public int Fat;
}

public record TwoFoldYaml : TestTaml
{
    public int Weezer;
}


public record TestTemplate : EntityTemplate
{
    public int Fat;

    public TestTemplate(TestTaml yaml, IReadOnlyDictionary<Type, object> components) : base(yaml, components)
    {
        Fat = yaml.Fat;
    }

    public override Type EntityType => typeof(TestEntType);
}

public record TestTemplateTwo : TestTemplate
{
    private int Weezer = 0;
    public TestTemplateTwo(TwoFoldYaml yaml, IReadOnlyDictionary<Type, object> components) : base(yaml, components)
    {
        Fat = yaml.Fat;
        Weezer = yaml.Weezer;
    }
}