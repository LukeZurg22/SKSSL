using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SKSSL.ECS;
using SKSSL.Extensions;
using SKSSL.Tests.TestData;
using static SKSSL.Tests.TestPrototypes;

// ReSharper disable RedundantNameQualifier
// ReSharper disable UnusedMember.Global

namespace SKSSL.Tests;

public abstract class TestPrototypes
{
    public static readonly string ExpectedOutputSingleEntry = $"""
                                                               - type: Entity
                                                                 id: testa
                                                                 name: test-name
                                                                 description: test-desc
                                                                 components:
                                                                 - type: {nameof(TestBlankComponent).Replace("Component", "")}
                                                               """;

    public static readonly string ExpectedOutputYamlMultiEntry = """
                                                                 - type: Entity
                                                                   id: testb
                                                                   name: test-name
                                                                   description: test-desc

                                                                 - type: Entity
                                                                   id: testc       
                                                                   name: test-name
                                                                   description: test-desc
                                                                 """;

    public static readonly string TestYamlSingleEntry = $"""
                                                         - type: Entity
                                                           id: testa         
                                                           name: test-name
                                                           description: test-desc
                                                           components:
                                                           - type: {nameof(TestBlankComponent).Replace("Component", "")}
                                                         """;

    public const string TestYamlMultiEntry = """
                                             - type: Entity
                                               id: testb          
                                               name: test-name
                                               description: test-desc
                                             - type: Entity
                                               id: testc        
                                               name: test-name
                                               description: test-desc
                                             """;

    public const string TestYamlOverride = """
                                           # Ensure that test-a has full qualifier
                                           - type: Entity
                                             id: game:testa
                                             name: test-name-override
                                             description: test-desc-override
                                           """;
}

[TestClass, UsedImplicitly]
[TestSubject(typeof(YamlLoader)), TestSubject(typeof(EntityManager))]
public class ECS
{
    #region Test Entries

    private static readonly ComponentYaml _flatComp = new() { Type = "TestComponent" };

    private static readonly ComponentYaml _fieldComp = new()
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
        var yml = yamlLoader.Deserialize(ExpectedOutputSingleEntry, "Test");

        // entity A
        Assert.IsNotEmpty(yml);
        _entities = [];
        yml.ForEach(prototype => _entities.AddRange(prototype as Entity));


        // entities B & C
        yml = yamlLoader.Deserialize(ExpectedOutputSingleEntry, "Test");
        yml.ForEach(prototype => _entities.AddRange(prototype as Entity));
        Assert.IsNotEmpty(yml);
    }

    [TestMethod, UsedImplicitly, TestSubject(typeof(YamlLoader))]
    public void TEST_COMPONENT_CONVERSIONS()
    {
        var component = new TestFieldComponent { x = 7 };
        try
        {
            // Testing component to yaml.
            ComponentYaml componentYaml = component.ToYaml();
            object first = componentYaml.Entries.Values.First();
            Assert.IsTrue((int)first == 7);

            // Testing the reverse after change..
            component = componentYaml.FromYaml() as TestFieldComponent;
            Assert.IsTrue(component != null, nameof(component) + " != null");
            Assert.IsTrue(component.x == 7);
        }
        catch (Exception e)
        {
            Assert.Fail(e.Message);
        }
    }

    [TestMethod, UsedImplicitly, TestSubject(typeof(YamlLoader))]
    public void TEST_YAML_SERIALIZER()
    {
        var loader = new SKSSL.YamlLoader();

        // Test serializing one entity.
        _entities.Clear();
        _entities.Add(_testEntityInstance);
        string output = loader.Serialize(_entities);
        Assert.IsFalse(string.IsNullOrEmpty(output), nameof(output) + " != null");

        // Deserialize as a list of prototypes and assume the first entry is the entity put in.
        var prototypes = loader.Deserialize(output, "Test");
        var entry = prototypes[0] as Entity;
        /* // Expected values
         * - handle = "test-entity",
         *   type = "Entity",
         *   yamlComponents =
         *   [
         *       _flatComp,
         *       _fieldComp
         *   ]
         */
        Assert.IsTrue(entry != null && entry == _testEntityInstance);

        _entities.Add(_testInheritedEntityInheritedInstance);
        output = loader.Serialize(_entities);
        Assert.IsFalse(string.IsNullOrEmpty(output), nameof(output) + " != null");
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
        var text = TestYamlSingleEntry.Split("\n");
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
        SKSSL.ECS.ECSMasterRegistry.Clear();
        Assert.IsEmpty(SKSSL.ECS.ECSMasterRegistry.TypeDefinitions);
    }
}