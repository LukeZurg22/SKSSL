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
        // Get fresh ID.
        int id = _nextId++;
        
        // Make the entity and feed it.
        var entity = new SKEntity(id, ComponentRegistry.Count)
        {
            ReferenceId = template.ReferenceId,
            NameKey = template.NameKey,
            DescriptionKey = template.DescriptionKey,
            World = world
        };
        
        // Add components to the entity.
        foreach ((Type type, object _) in template.DefaultComponents)
            entity.AddComponent(type);

        return entity;
    }

    public SKEntity Spawn(string referenceId, BaseWorld? world = null)
    {
        EntityTemplate template = EntityRegistry.GetTemplate(referenceId);
        SKEntity entity = CreateEntity(template);
        _allEntities.Add(entity);
        return entity;
    }
}