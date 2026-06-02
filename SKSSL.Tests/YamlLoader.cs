using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SKSSL.ECS;
using SKSSL.Extensions;
using SKSSL.Tests.TestData;
using SKSSL.YAML;

namespace SKSSL.Tests;

[TestClass, TestSubject(typeof(SKSSL.YAML.YamlLoader))]
public class YamlLoader
{
    /*
     * Can serialize entity as prototype, but one cannot load the base prototype as an Entity.
     * Setup is repeated per-tests.
     */

    private List<Entity> _entities;

    [TestInitialize, UsedImplicitly]
    public void Initialize()
    {
        var lines = TestYaml.ExpectedOutputSingleEntry.Replace("\r", "").Split('\n');
        var yml = YAML.YamlLoader.DeserializePrototypesFrom(lines, "Test");

        // entity A
        Assert.IsNotEmpty(yml); // Testing, just in case!
        _entities = [];
        yml.ForEach(prototype=>_entities.AddRange(prototype as Entity));
        
        // entities B & C
        lines = TestYaml.ExpectedOutputYamlMultiEntry.Replace("\r", "").Split('\n');
        yml = YAML.YamlLoader.DeserializePrototypesFrom(lines, "Test");
        yml.ForEach(prototype=>_entities.AddRange(prototype as Entity));
    }


    readonly YamlComponent _testYamlComponent = new()
    {
        Type = "TestField",
        Entries =
        {
            { "x", 1 },
            { "y", 2 }
        }
    };

    [TestMethod, UsedImplicitly]
    public void TEST_YAML_CONVERSIONS()
    {
        try
        {
            var component = _testYamlComponent.FromYaml() as TestFieldComponent;
            Assert.IsTrue(component != null, nameof(component) + " != null");
            Assert.IsTrue(component.x == 1 && component.y == 2);
        }
        catch (Exception e)
        {
            Assert.Fail(e.Message);
        }
    }

    [TestMethod, UsedImplicitly]
    public void TEST_YAML_CONVERT_TO_FROM_YAML()
    {
        var component = new TestFieldComponent { x = 7 };
        try
        {
            // Testing component to yaml.
            YamlComponent yamlComponent = component.ToYaml();
            object first = yamlComponent.Entries.Values.First();
            Assert.IsTrue((int)first == 7);

            // Testing the reverse after change..
            component = yamlComponent.FromYaml() as TestFieldComponent;
            Assert.IsTrue(component != null, nameof(component) + " != null");
            Assert.IsTrue(component.x == 7);
        }
        catch (Exception e)
        {
            Assert.Fail(e.Message);
        }
    }

    #region Test Entities

    private readonly Entity _testEntityInstance = new()
    {
        Handle = "test-entity",
        Type = "Entity",
        YamlComponents =
        [
            new YamlComponent { Type = "TestComponent" },
            new YamlComponent
            {
                Type = "TestComponent2",
                Entries =
                {
                    ["x"] = 10,
                    ["y"] = 20
                }
            }
        ]
    };

    private readonly TestEntityInheritedType _testInheritedEntityInheritedInstance = new()
    {
        Handle = "test-entity-inherited",
        Type = "TestEntityType",
        TestString = "my-test-string",
        YamlComponents =
        [
            new YamlComponent { Type = "TestComponent" },
            new YamlComponent
            {
                Type = "TestComponent2",
                Entries =
                {
                    ["x"] = 10,
                    ["y"] = 20
                }
            }
        ]
    };

    #endregion

    [TestMethod, UsedImplicitly]
    public void TEST_YAML_COMPONENT_SERIALIZER()
    {
        string output = "";
        _entities.Clear();
        // Line endings and so-forth aren't important here. All that matters is that the data serializes as expected.
        var testComponent = new YamlComponent { Type = "TestFieldComponent" };
        _entities.Add(_testEntityInstance);
        output = YAML.YamlLoader.Serialize(_entities);
        _entities.Add(_testInheritedEntityInheritedInstance);
        output = YAML.YamlLoader.Serialize(_entities);
    }

    [TestMethod, UsedImplicitly]
    public void TEST_YAML_ENTITY_SERIALIZER()
    {
        // [0] must equal an entity
        Entity entityA = _entities[0];
        var strA = YAML.YamlLoader.Serialize(entityA).ReplaceLineEndings("").Replace(" ", "");
        var compA = TestYaml.ExpectedOutputSingleEntry;
        Assert.IsTrue(strA.Equals(compA));

        Entity[] entityBC = [_entities[1], _entities[2]];
        var strB = YAML.YamlLoader.Serialize(entityBC).ReplaceLineEndings("").Replace(" ", "");
        var compB = TestYaml.ExpectedOutputYamlMultiEntry;
        Assert.IsTrue(strB.Equals(compB));
    }

    [TestMethod, UsedImplicitly]
    public void TEST_YAML_COMPONENTS()
    {
    }
}