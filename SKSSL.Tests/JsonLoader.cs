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

[TestClass, TestSubject(typeof(ConsQJsonLoader))]
public class JsonLoader
{
    /*
     * Can serialize entity as prototype, but one cannot load the base prototype as an Entity.
     * Setup is repeated per-tests.
     */

    private List<Entity> _entities;

    [TestInitialize, UsedImplicitly]
    public void Initialize()
    {
        var loader = new ConsQJsonLoader();
        var protoList = loader.Deserialize(TestJSON.ExpectedOutputSingleEntryJSON, "Test");

        // entity A
        Assert.IsNotEmpty(protoList);
        _entities = [];
        protoList.ForEach(prototype => _entities.AddRange(prototype as Entity));
    }


    private readonly ComponentProto _testComponentProto = new()
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
            var component = _testComponentProto.FromYaml() as TestFieldComponent;
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
            ComponentProto componentProto = component.ToYaml();
            object first = componentProto.Entries.Values.First();
            Assert.IsTrue((int)first == 7);

            // Testing the reverse after change..
            component = componentProto.FromYaml() as TestFieldComponent;
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
            new ComponentProto { Type = "TestComponent" },
            new ComponentProto
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
            new ComponentProto { Type = "TestComponent" },
            new ComponentProto
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


}