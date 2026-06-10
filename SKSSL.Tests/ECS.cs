using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SKSSL.ECS;
using SKSSL.Extensions;
using SKSSL.Tests.TestData;
using SKSSL.YAML;

// ReSharper disable RedundantNameQualifier

// ReSharper disable UnusedMember.Global

namespace SKSSL.Tests;

[TestClass, UsedImplicitly]
[TestSubject(typeof(YamlLoader)), TestSubject(typeof(EntityManager))]
public class ECS
{
    #region Test Entities

    private static readonly ComponentProto _flatComp = new ComponentProto { Type = "TestComponent" };

    private static readonly ComponentProto _fieldComp = new()
    {
        Type = "TestField",
        Entries =
        {
            { "x", 1 },
            { "y", 2 }
        }
    };

    private readonly Entity _testEntityInstance = new()
    {
        Handle = "test-entity",
        Type = "Entity",
        YamlComponents =
        [
            _flatComp,
            _fieldComp
        ]
    };

    private readonly TestEntityInheritedType _testInheritedEntityInheritedInstance = new()
    {
        Handle = "test-entity-inherited",
        Type = "TestEntityType",
        TestString = "my-test-string",
        YamlComponents =
        [
            _flatComp,
            _fieldComp
        ]
    };

    #endregion

    /*
     * Can serialize entity as prototype, but one cannot load the base prototype as an Entity.
     * Setup is repeated per-tests.
     */

    private List<Entity> _entities;

    [TestInitialize, UsedImplicitly]
    public void Initialize()
    {
        var yamlLoader = new SKSSL.YamlLoader();
        var yml = yamlLoader.Deserialize(SKSSL.Tests.TestData.TestPrototypes.ExpectedOutputSingleEntry, "Test");

        // entity A
        Assert.IsNotEmpty(yml);
        _entities = [];
        yml.ForEach(prototype => _entities.AddRange(prototype as Entity));


        // entities B & C
        yml = yamlLoader.Deserialize(SKSSL.Tests.TestData.TestPrototypes.ExpectedOutputSingleEntry, "Test");
        yml.ForEach(prototype => _entities.AddRange(prototype as Entity));
    }

    [TestMethod, UsedImplicitly]
    public void TEST_YAML_CONVERSIONS()
    {
        try
        {
            var component = _fieldComp.FromYaml() as TestFieldComponent;
            Assert.IsTrue(component != null, nameof(component) + " != null");
            Assert.IsTrue(component.x == 1 && component.y == 2);
        }
        catch (Exception e)
        {
            Assert.Fail(e.Message);
        }
    }

    [TestMethod, UsedImplicitly, TestSubject(typeof(YamlLoader))]
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

    [TestMethod, UsedImplicitly, TestSubject(typeof(YamlLoader))]
    public void TEST_YAML_COMPONENT_SERIALIZER()
    {
        string output = "";
        var yamlLoader = new SKSSL.YamlLoader();
        _entities.Clear();
        // Line endings and so-forth aren't important here. All that matters is that the data serializes as expected.
        var testComponent = new ComponentProto { Type = "TestFieldComponent" };
        _entities.Add(_testEntityInstance);
        output = yamlLoader.Serialize(_entities);
        _entities.Add(_testInheritedEntityInheritedInstance);
        output = yamlLoader.Serialize(_entities);
    }

    [TestMethod, UsedImplicitly, TestSubject(typeof(YamlLoader))]
    public void TEST_YAML_ENTITY_SERIALIZER()
    {
        var yamlLoader = new SKSSL.YamlLoader();
        // [0] must equal an entity
        Entity entityA = _entities[0];
        var strA = yamlLoader.Serialize(entityA).ReplaceLineEndings("").Replace(" ", "");
        var compA = SKSSL.Tests.TestData.TestPrototypes.ExpectedOutputSingleEntry;
        Assert.IsTrue(strA.Equals(compA));

        Entity[] entityBC = [_entities[1], _entities[2]];
        var strB = yamlLoader.Serialize(entityBC).ReplaceLineEndings("").Replace(" ", "");
        var compB = SKSSL.Tests.TestData.TestPrototypes.ExpectedOutputYamlMultiEntry;
        Assert.IsTrue(strB.Equals(compB));
    }
    
    // TODO: The same but for JSON format.

    /// Source Generator yields the expectation that one of the Test Systems are present.
    [TestMethod]
    public void TEST_SYSTEM_REGISTRY() => Assert.IsNotEmpty(SKSSL.ECS.SystemManager.AllSystems);

    [TestMethod]
    public void TEST_COMPONENT_REGISTRY()
    {
        // There should be more than zero components registered, given the test components exist from Source Gen.
        Assert.IsGreaterThan(0, SKSSL.ECS.ComponentRegistry.Count);
        Assert.IsGreaterThan(0, SKSSL.ECS.ComponentRegistry.RegisteredTypeIDDictionary.Count);
        Assert.IsGreaterThan(0, SKSSL.ECS.ComponentRegistry.RegisteredHandleComponentTypesDictionary.Count);
    }

    /// Register entity from single file, single entry.
    /// Register multiple types from one file.
    /// Register one file, then test override.
    [TestMethod]
    public void TEST_ENTITY_REGISTRY()
    {
        var text = SKSSL.Tests.TestData.TestPrototypes.TestYamlSingleEntry.Split("\n");
        //var d = SKSSL.YAML.GameContentLoader.DeserializePrototypesFrom(text, "Test");
        // Assert single.
        
        // Assert multiple.
        
        // Assert override as expected.
        
        // Assert override w. bad override handle.
    }

    [TestMethod]
    public void TEST_ENTITY_SPAWN()
    {
        // Assert test spawn.
        
        // Assert test clone of spawn.
        // TODO:
        //  Spawn entity.
        //  Modify it.
        //  Clone it.
    }

    [TestMethod]
    public void TEST_COMPONENT()
    {
        // Add component.
        
        // Remove component.
    }

    [TestMethod]
    public void TEST_ENTITY_INHERITANCE()
    {
    }

    [TestMethod]
    public void TEST_EVENT_SUBSCRIPTION()
    {
    }

    [TestMethod]
    public void TEST_EVENT_CALLBACK()
    {
    }

    [TestMethod]
    public void TEST_PROTO_REGISTRY_CLEAR()
    {
        SKSSL.ECS.GameECSMasterRegistry.Clear();
        Assert.IsEmpty(SKSSL.ECS.GameECSMasterRegistry.TypeDefinitions);
    }
}