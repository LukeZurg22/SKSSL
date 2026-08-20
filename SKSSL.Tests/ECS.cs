using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SKSSL.ECS;
using SKSSL.ECS.Registry;
using SKSSL.Exceptions;
using SKSSL.Extensions;
using SKSSL.Serializing;
using SKSSL.Tests.TestData;
using static Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
using static SKSSL.Tests.TestPrototypes;

// ReSharper disable RedundantNameQualifier
// ReSharper disable UnusedMember.Global

namespace SKSSL.Tests;

[TestClass, UsedImplicitly]
[TestSubject(typeof(YamlSerializerSolKom)), TestSubject(typeof(EntityManager))]
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

    private readonly PrototypeLoader<YamlSerializerSolKom> _loader = new();
    private readonly EntityRegistry _entityRegistry = MasterRegistryManager.GetRegistry<Entity, EntityRegistry>();


    [TestMethod, UsedImplicitly, TestSubject(typeof(PrototypeLoader<YamlSerializerSolKom>))]
    public void TEST_PROTOTYPE_LOADING()
    {
        List<Entity> entities = [];

        // entity A
        var yml = _loader.Deserialize(TestYamlOutputSingleEntry, "Test");
        HasCount(1, yml); // Test deserialize. Only one entry expected.
        IsNotEmpty(yml);
        yml.ForEach(prototype => entities.AddRange(prototype as Entity));

        // entities B & C
        yml = _loader.Deserialize(TestYamlMultiEntry, "Test");
        HasCount(2, yml);
        yml.ForEach(prototype => entities.AddRange(prototype as Entity));

        // Test serializing and de-serializing in one breath.
        entities.Clear();
        entities.Add(_testEntityInstance);
        string output = _loader.Serialize(entities);
        IsFalse(string.IsNullOrEmpty(output), nameof(output) + " != null");

        // Deserialize as a list of prototypes and assume the first entry is the entity put in.
        var prototypes = _loader.Deserialize(output, "Test");
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
        output = _loader.Serialize(entities);
        Contains(ExpectedTestString, output); // Ensure that an expected variable is contained.
    }


    [TestMethod, UsedImplicitly, TestSubject(typeof(YamlSerializerSolKom))]
    public void TEST_COMPONENT_CONVERSIONS()
    {
        var component = new TestFieldComponent { x = 7 };

        // Testing component to yaml.
        ComponentYaml componentYaml = component.ToYaml();
        object first = componentYaml.Entries.Values.First();
        AreEqual(7, (int)first);

        // Testing the reverse after change..
        component = componentYaml.FromYaml() as TestFieldComponent;
        IsTrue(component != null, nameof(component) + " != null");
        AreEqual(7, component.x);
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
        const string TestEntHandleA = "TestEntityA";
        const string TestEntHandleB = "TestEntityB";
        // -> Default source is "game"

        // Assert single registered. Also assumes that the type inheritance and derivation are handled appropriately.
        var testEntA = new TestPrototypeSingle { Handle = TestEntHandleA };
        MasterRegistryManager.TryRegisterPrototype(testEntA);
        IsTrue(MasterRegistryManager.TryGetPrototype($"game:{TestEntHandleA}", out _));

        // Assert multiple.
        var testEntB = new TestPrototypeBlank { Handle = TestEntHandleB };
        MasterRegistryManager.TryRegisterPrototype(testEntB);
        IsTrue(MasterRegistryManager.TryGetPrototype($"game:{TestEntHandleB}", out _));

        // Assert override as expected.
        var testEntC = new TestPrototypeInherit { Handle = "override_me", FirstField = 0 };
        MasterRegistryManager.TryRegisterPrototype(testEntC);
        var @override = new TestPrototypeInherit
            { Source = "other", Handle = "override_me", Replace = "game", FirstField = 99 };
        IsTrue(MasterRegistryManager.TryRegisterPrototype(@override));

        // Assert override w. bad override handle. // "override_me" exists in the registry, at the moment.
        var testEntD = new TestPrototypeInherit { Replace = "other", Handle = "invalid_handle", FirstField = 0 };
        Throws<RegistryException>(() => MasterRegistryManager.TryRegisterPrototype(testEntD));
    }

    [TestMethod]
    public void TEST_UID_GEN()
    {
        UidList<object> uids = [];
        const byte ExpectedEntries = 6;

        // Dummy object for testing.
        for (int i = 0; i < ExpectedEntries; i++)
        {
            object dummy = new();
            string head = $"test_{i % 2}";
            PackableUid uid = uids.New();
            uids.Set(dummy, uid, head);
        }

        // Asserting with six entries GetAll
        HasCount(ExpectedEntries, uids);
        HasCount(ExpectedEntries / 2, uids.GetAll("test_0"));
        HasCount(ExpectedEntries / 2, uids.GetAll("test_1"));

        // Test removal and replacement.
        uids.Destroy(new PackableUid(0, 1));
        PackableUid replace = uids.New();
        uids.Set(new object(), replace, "test_0");
        IsTrue(replace.Index == 0 && replace.Generation == 2, nameof(replace) + " invalid index and generation");
        uids.Clear();
    }

    private readonly EntityManager _entityManager = new();

    [TestMethod]
    public void TEST_ENTITIES_COMPONENTS()
    {
        // ===TEST ENTITY SPAWNING===
#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
        // Word of warning for this test: An instanced Entity Manager is decoupled, meaning that the ECS Entity context
        //  won't be 100% reliable, and reflective methods that extend Entity functions like ComponentRegistry.Add()
        //  will not work perfectly given the context. This is being corrected, and may be fixed already.

        var entity = new TestEntityInheritedType { TestString = "Test Successful" };
        _entityRegistry.Register("TestEntity", entity);

        // Assert test spawn.
        var spawnedEntity = _entityManager.Spawn("TestEntity") as TestEntityInheritedType;
        IsNotNull(spawnedEntity); // Entity should have spawned successfully.
        AreEqual(spawnedEntity.TestString, entity.TestString);

        spawnedEntity.TestString = "Test Also Successful";
        
        // Assert test clone of spawn.
        // Note: It's assumed that the entity by this point was already cloned, but cloning from an existing one using
        //  the internal function is what this intends to test. This cloning shouldn't be done anyway, but this is to
        //  test the Cloning directly, the proper way.
        var clone = _entityManager.Clone(spawnedEntity) as TestEntityInheritedType;
        IsNotNull(clone); // Entity should have cloned successfully.
        AreEqual(clone.TestString, spawnedEntity.TestString);
#pragma warning restore CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
        
        // TESTING COMPONENTS
        // Add component.
        spawnedEntity.AddComponent(new TestBlankComponent());
        // WIP: ADDING TEST CASES FOR COMPONENTS. I WENT ON A BINGE CLEANING UP THE ENTITY CONTEXT. WOOPS!
        
        
        // Remove component.
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