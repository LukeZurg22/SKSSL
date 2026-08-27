using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using MemoryPack;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using SKSSL.Extensions;
using SKSSL.Scenes;
using SKSSL.Serializing;
using YamlDotNet.Serialization;

namespace SKSSL.ECS;

/// <summary>
/// Instanced Entity representing an object present within game memory. Entities are contained within a
/// <see cref="ParentWorld"/>, and contain <see cref="ComponentRegistry"/> Component Indices for pointing to component arrays.
/// </summary>
/// <inheritdoc cref="Prototype"/>
/// <code>
/// In Addition To:
///   components: (Component Yaml Entries)
///     - type: (string)
///       field_1: (varies)
///       field_2: (varies)
///       field_3: (varies)
/// # (Note: Component fields vary between component type.)
/// </code>
[JsonObject]
public class Entity : Prototype, InternalUidObject<EntityUid>, ICloneable<Entity>
{
    /// Reverse-reference back to the world that this entity inhabits.
    [MemoryPackIgnore, YamlIgnore, System.Text.Json.Serialization.JsonIgnore]
    public EntityManager SourceManager { get; set; }

    /// Reverse-reference back to the world that this entity inhabits.
    [MemoryPackIgnore, YamlIgnore, System.Text.Json.Serialization.JsonIgnore]
    public World ParentWorld { get; set; }

    public EntityContext Context => new(this);


    [YamlMember(Alias = "abstract", Order = 1), JsonProperty(nameof(Abstract))]
    public bool Abstract { get; set; } = false;

    /// Parentage Field, where the properties of the parent are introduced to the child.
    /// Child overrides parent properties. This replaces the templating system of olde.
    [YamlMember(Alias = "inherit", Order = 2), JsonProperty("Inherit")]
    public string[] Inherit = [];

    // -> Handle (Order 3)

    /*
     * All Entities are expected to have name and description keys provided. This isn't as limiting as it seems.
     * Inheriting directly from the root Prototype record allows one to create a new prototype definition which does
     * not need to contain a name.
     */

    // TEMP: Consider moving these qualities into some MetaData type.
    // TODO: Wrap these strings with a "LocaleKey" type w. a function ".Localized()"

    #region Name / Description / Meta-Data

    /// Non-localized name key.
    [YamlMember(Alias = "name", Order = 4), JsonProperty("Name")]
    public LocKey Name = new(string.Empty);

    /// Non-localized description key.
    [YamlMember(Alias = "description", Order = 5), JsonProperty("Description")]
    public LocKey Description = new(string.Empty);

    #endregion

    /// [De]serialized component entries part of this prototype. Put on end of order.
    [YamlMember(Alias = "components", Order = 99)]
    public List<ComponentYaml>? YamlComponents = [];

    /// Exclaim if this entity and its components require updating.
    [MemoryPackIgnore, YamlIgnore, System.Text.Json.Serialization.JsonIgnore]
    internal bool IsDirty;

    #region Constructors & UID

    /// Constructor for flat "empty" Entity. NOT recommended without special handling for Entity's fields.
    [MemoryPackConstructor, System.Text.Json.Serialization.JsonConstructor]
    public Entity()
    {
    }

    public Entity(EntityUid uid) => _uid = uid;

    /// An entity must keep its UID consistently. Do NOT manually assign this!
    [MemoryPackIgnore, YamlIgnore, System.Text.Json.Serialization.JsonIgnore]

    internal EntityUid _uid { get; private set; }

    public void SetUid(EntityUid uid) => _uid = uid;

    public EntityUid GetUid() => _uid;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    /// <returns></returns>
    /// <exception cref="NullReferenceException">
    /// A null exception occurs when an entity not "properly" initialized is acted-upon by the Component Registry,
    /// or any system that involves a Uid that may be null.
    /// </exception>
    public static implicit operator EntityUid(Entity entity) => entity._uid;

    #endregion

    /// <summary>
    /// Effective cloning from one source, copied into this entity. Override & add base call at top of function
    /// followed by additional copied-fields for any entity definitions that require their own implementation of
    /// this method.
    /// </summary>
    public virtual Entity CopyFrom(Entity source)
    {
        base.CopyFrom(source); // Copy base-prototype stuff.
        Abstract = source.Abstract;
        Inherit = source.Inherit;
        Name = source.Name;
        Description = source.Description;
        SourceManager = source.SourceManager;
        ParentWorld = source.ParentWorld;
        if (source.YamlComponents != null && YamlComponents != null)
            YamlComponents = YamlComponents
                .Select(c => c.Clone())
                .ToList();
        else
            YamlComponents = [];

        return this;
    }

    /// <summary>
    /// Clones an entity. Uid is NOT copied. Do NOT call this directly upon an entity!
    /// Use <see cref="EntityManager.Clone"/> instead.
    /// </summary>
    public virtual Entity Clone() => new Entity().CopyFrom(this);

    /// <summary>
    /// Special initialization logic for Entity.
    /// </summary>
    /// <remarks>
    /// This is called from <see cref="EntityManager.Spawn(string)"/>; as in on-spawn.
    /// <br/><br/>
    /// Systems will automatically act upon an entity's components, this method is a formality for special
    /// alternative behaviour on-creation.
    /// </remarks>
    public virtual void Initialize()
    {
    }

    /// Special draw instructions per-entity, should a Rendering component not be enough.
    public virtual void Draw(SpriteBatch spriteBatch)
    {
    }

    /// Special entity behaviour / status update call, should behavioural components not be enough.
    public virtual void Update(GameTime gameTime)
    {
    }

    #region Operators

    /// <summary>
    /// Equate Uids, Written-Types, & Handles. More thorough.
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    protected bool Equals(Entity other) => _uid.Packed == other.GetUid().Packed &&
                                           Type.Equals(other.Type) &&
                                           Handle.Equals(other.Handle);

    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        return obj.GetType() == GetType() && Equals((Entity)obj);
    }

    [SuppressMessage("ReSharper", "NonReadonlyMemberInGetHashCode")]
    public override int GetHashCode() => HashCode.Combine(Handle, Inherit, IsDirty, ParentWorld, _uid);


    /// Equates handles only.
    public static bool operator ==(Entity? a, Entity? b)
        => ReferenceEquals(a, b) || a is not null && b is not null && a.Handle == b.Handle;

    public static bool operator !=(Entity? a, Entity? b) => !(a == b);

    #endregion
}