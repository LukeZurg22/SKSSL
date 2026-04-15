// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedTypeParameter
// ReSharper disable VirtualMemberNeverOverridden.Global
namespace SKSSL.ECS;

/// <summary>
/// Base class for entity system registration. Append <see cref="RegisterSystemAttribute"/> for game to auto-register
/// this system on Initialize.
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
    /// Quick route to SSLGame.ECS().World.Events
    /// </summary>
    public static EventHandler Events => Context.World.Events;
    
    /// <summary>
    /// Public class so the system stops complaining.
    /// </summary>
    public EntitySystem()
    {
    }

    /// <summary>
    /// Overridable initialize method that allows local events to be subscript to the Event Handler.
    /// </summary>
    public virtual void Initialize()
    {
    }

    protected void SubscribeLocalEvent<E>(Action<int, E> handler) where E : EntityEvent => Events.Subscribe(handler);
    protected void RaiseLocalEvent<E>(int uid, E @event) where E : EntityEvent => Events.Raise(uid, @event);
    protected void RaiseLocalEvent<E>(SKEntity ent, E @event) where E : EntityEvent => Events.Raise(ent.Id, @event);
}