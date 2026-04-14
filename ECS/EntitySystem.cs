namespace SKSSL.ECS;

/// <summary>
/// Base class for entity system registration.
/// </summary>
public abstract class EntitySystem
{
    /// <summary>
    /// Quick Context route to SSLGame.ECS();
    /// </summary>
    /// <remarks>
    /// Will cause a crash if ECS isn't enabled, and Entity Systems are in use.
    /// </remarks>
    protected static EntityContext Context => SSLGame.ECS();
    
    /// <summary>
    /// Public class so the system stops complaining.
    /// </summary>
    public EntitySystem()
    {
    }
}
