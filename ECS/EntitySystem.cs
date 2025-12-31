using SKSSL.Scenes;

namespace SKSSL.ECS;

/// <summary>
/// Base class for entity system registration.
/// </summary>
public abstract class EntitySystem
{
    public BaseWorld World { set; get; } = null!;
    public readonly BaseWorld? _world;

    /// <summary>
    /// Instantiated constructor mostly used by the automated registration system.
    /// </summary>
    /// <param name="world"></param>
    public EntitySystem(BaseWorld? world) => _world = world;

    /// <summary>
    /// Public class so the system stops complaining.
    /// </summary>
    public EntitySystem()
    {
    }
    
    
}
