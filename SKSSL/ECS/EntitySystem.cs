using Microsoft.Xna.Framework.Graphics;
using SKSSL.Extensions;
using SKSSL.Scenes;

// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedTypeParameter
// ReSharper disable VirtualMemberNeverOverridden.Global
// ReSharper disable PublicConstructorInAbstractClass

namespace SKSSL.ECS;

/// <summary>
/// Base class for entity system registration. Append <see cref="RegisterSystemAttribute"/> for game to auto-register
/// this system on Initialize.
/// </summary>
public abstract class EntitySystem
{
    #region Shortcut Functions

    /// <summary>
    /// Quick Context route to SSLGame.ECS();
    /// </summary>
    /// <remarks>
    /// Will cause a crash if ECS isn't enabled, and Entity Systems are in use.
    /// </remarks>
    protected static EntityContext Context => SSLGame.ECS();

    /// <summary>
    /// Quick route to SSLGame.ECS().EntityManager.
    /// </summary>
    protected static EntityManager EntityManager => Context.EntityManager;

    /// <summary>
    /// Quick route to SSLGame.ECS().World for extension methods.
    /// </summary>
    public static BaseWorld World => Context.World;

    /// <summary>
    /// Quick route to SSLGame.ECS().World.Events
    /// </summary>
    public static EventHandler Events => Context.World.Events;

    /// <summary>
    /// Quick route to SSLGame.ECS().World.Graphics handling.
    /// </summary>
    public static GraphicsDevice Graphics => Context.World.Graphics.GraphicsDevice;

    /// <summary>
    /// Quick route to World.QueryComponents Query.
    /// </summary>
    public static IEnumerable<T> Query<T>() where T : ISKComponent => World.QueryComponents<T>();

    #endregion

    /// <summary>
    /// Public constructor so the system stops complaining.
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