using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SKSSL.ECS;
using SKSSL.ECS.Registry;
using SKSSL.Extensions;
using SKSSL.Tests.TestData;
using static Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
using static SKSSL.Tests.TestPrototypes;

// ReSharper disable RedundantNameQualifier
// ReSharper disable UnusedMember.Global

namespace SKSSL.Tests;

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

    private const string ExpectedTestString = "my-test-string";

    private readonly TestEntityInheritedType _testInheritedEntityInheritedInstance = new()
    {
        Handle = "test-entity-inherited",
        Type = "TestEntityType",
        TestString = $"{ExpectedTestString}",
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

    private readonly PrototypeLoader _prototypeLoader = new SKSSL.YamlLoader();

    [TestMethod, UsedImplicitly, TestSubject(typeof(PrototypeLoader))]
    public void TEST_PROTOTYPE_LOADING()
    {
        List<Entity> entities = [];

        // entity A
        var yml = _prototypeLoader.Deserialize(TestYamlOutputSingleEntry, "Test");
        HasCount(1, yml); // Test deserialize. Only one entry expected.
        IsNotEmpty(yml);
        yml.ForEach(prototype => entities.AddRange(prototype as Entity));

        // entities B & C
        yml = _prototypeLoader.Deserialize(TestYamlMultiEntry, "Test");
        HasCount(2, yml);
        yml.ForEach(prototype => entities.AddRange(prototype as Entity));

        // Test serializing and de-serializing in one breath.
        entities.Clear();
        entities.Add(_testEntityInstance);
        string output = _prototypeLoader.Serialize(entities);
        IsFalse(string.IsNullOrEmpty(output), nameof(output) + " != null");

        // Deserialize as a list of prototypes and assume the first entry is the entity put in.
        var prototypes = _prototypeLoader.Deserialize(output, "Test");
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
        IsTrue(entry != null && entry == _testEntityInstance);

        // Test special inherited type.
        entities.Add(_testInheritedEntityInheritedInstance);
        output = _prototypeLoader.Serialize(entities);
        Contains(ExpectedTestString, output); // Ensure that an expected variable is contained.
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
            AreEqual(7, (int)first);

            // Testing the reverse after change..
            component = componentYaml.FromYaml() as TestFieldComponent;
            IsTrue(component != null, nameof(component) + " != null");
            AreEqual(7, component.x);
        }
        catch (Exception e)
        {
            Fail(e.Message);
        }
    }

    // TODO: The same but for JSON format.

    /// Source Generator yields the expectation that one of the Test Systems are present. Easy!
    [TestMethod]
    public void TEST_REGISTRY_COUNTS()
    {
        IsNotEmpty(SKSSL.ECS.SystemManager.AllSystems);
        IsGreaterThan(0, SKSSL.ECS.ComponentRegistry.Count);
        IsGreaterThan(0, SKSSL.ECS.ComponentRegistry.RegisteredTypeIDDictionary.Count);
        IsGreaterThan(0, SKSSL.ECS.ComponentRegistry.RegisteredHandleComponentTypesDictionary.Count);
    }

    /// Register entity from single file, single entry.
    /// Register multiple types from one file.
    /// Register one file, then test override.
    [TestMethod]
    public void TEST_ENTITY_REGISTRY()
    {
        List<Entity> entities = [];
        const string TestEntHandle  = "TestEntity";
        
        // Assert single register.
        TestEntityInheritedType testEnt = new TestEntityInheritedType { Handle = TestEntHandle };
        MasterRegistryManager.RegisterLoadedPrototype(testEnt.GetType(), testEnt);
        IsTrue(MasterRegistryManager.TryGetPrototype(TestEntHandle, out _));
        
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
        MasterRegistryManager.Clear();
        IsEmpty(MasterRegistryManager.TypeDefinitions);
    }
}