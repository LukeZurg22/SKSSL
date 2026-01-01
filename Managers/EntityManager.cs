using System.Reflection;
using SKSSL.ECS;
using SKSSL.Scenes;
using SKSSL.YAML;
using ComponentRegistry = SKSSL.ECS.ComponentRegistry;

namespace SKSSL.Managers;

public class EntityManager
{
    private static int _nextId = 0;
    private readonly List<SKEntity> _allEntities = [];

    public void WipeAllEntities()
    {
        _allEntities.Clear();
    }
    
    /// <summary>
    /// All entities that are active and exist somewhere.
    /// </summary>
    public IReadOnlyList<SKEntity> AllEntities => _allEntities;

    public SKEntity? GetEntity(string referenceId) => _allEntities.FirstOrDefault(e => e.ReferenceId == referenceId);

    /// <summary>
    /// TryGet wrapper for <see cref="GetEntity"/>
    /// </summary>
    public bool TryGetEntity(string referenceId, out SKEntity? entity)
    {
        entity = GetEntity(referenceId);
        return entity != null;
    }
    
    /// <summary>
    /// Creates a new entity and returns its handle.
    /// Optionally fills metadata from a template or explicit values.
    /// </summary>
    private static SKEntity CreateEntity(EntityTemplate template, BaseWorld? world = null)
    {
        int id = _nextId++;

        // Use the template's desired entity type
        var entity = (SKEntity)Activator.CreateInstance(
            template.EntityType,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            null,
            [id, ComponentRegistry.Count, template],
            null)! ?? throw new InvalidOperationException($"Failed to create entity \"{template.ReferenceId}\" in {nameof(CreateEntity)}");

        // Make a copy of the entity and force the reference ID. Funky, but it works.
        entity.World = world;

        foreach ((Type type, object _) in template.DefaultComponents)
            entity.AddComponent(type);

        return entity;
    }

    /// <summary>
    /// Acquires an entity template using a provided reference id, and creates an entity instance using it.
    /// </summary>
    /// <param name="referenceId">Reference id to template stored in registry.</param>
    /// <param name="world">Optional world parameter to define what world this entity is present in.</param>
    /// <returns>Spawned entity for later use.</returns>
    public SKEntity Spawn(string referenceId, BaseWorld? world = null)
    {
        EntityTemplate template = EntityRegistry.GetTemplate(referenceId);
        SKEntity entity = CreateEntity(template, world);
        _allEntities.Add(entity);
        return entity;
    }
}