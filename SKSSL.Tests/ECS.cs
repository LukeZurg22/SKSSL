using JetBrains.Annotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SKSSL.Tests.TestData;

// ReSharper disable RedundantNameQualifier

// ReSharper disable UnusedMember.Global

namespace SKSSL.Tests;

[TestClass, UsedImplicitly]
//[TestSubject(typeof())]
public class ECS
{
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
    [TestMethod]
    public void TEST_ENTITY_REGISTRY_SINGLE()
    {
        var text = TestYaml.TestYamlSingleEntry.Split("\n");
        var d = SKSSL.YAML.YamlLoader.DeserializePrototypesFrom(text, "Test");
    }

    /// Register multiple types from one file.
    [TestMethod]
    public void TEST_ENTITY_REGISTRY_MULTIPLE()
    {
    }

    /// Register one file, then test override.
    [TestMethod]
    public void TEST_ENTITY_REGISTRY_OVERRIDE()
    {
    }

    [TestMethod]
    public void TEST_MANUAL_ENTITY_REGISTRY()
    {
    }

    [TestMethod]
    public void TEST_ENTITY_SPAWN()
    {
    }

    [TestMethod]
    public void TEST_ENTITY_CLONE()
    {
        // TODO:
        //  Spawn entity.
        //  Modify it.
        //  Clone it.
    }

    [TestMethod]
    public void TEST_COMPONENT_ADD()
    {
    }

    [TestMethod]
    public void TEST_COMPONENT_REMOVE()
    {
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