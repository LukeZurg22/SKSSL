using System;
using System.Collections.Generic;
using System.Linq;
using MemoryPack;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using SKSSL.Extensions;
using SKSSL.Scenes;
using YamlDotNet.Serialization;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global


// ReSharper disable VirtualMemberCallInConstructor
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable VirtualMemberNeverOverridden.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable RedundantBaseConstructorCall
// ReSharper disable ClassNeverInstantiated.Global

namespace SKSSL.ECS;

/// <summary>
/// Instanced Entity representing an object present within game memory. Entities are contained within a
/// <see cref="World"/>, and contain <see cref="ComponentRegistry"/> Component Indices for pointing to component arrays.
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
public class Entity : Prototype
{
    /// <inheritdoc cref="Prototype.Type"/>
    [YamlMember(Alias = "type", Order = 0), JsonProperty(nameof(Type))]
    public override string Type { get; set; } = "Entity";

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

    #region Name / Description

    /// Non-localized name key.
    [YamlMember(Alias = "name", Order = 4), JsonProperty("Name")]
    public string? NameKey;

    /// <returns>Localized name from Name Key.</returns>
    public void GetName() => Loc.Get(NameKey);

    /// Non-localized description key.
    [YamlMember(Alias = "description", Order = 5), JsonProperty("Description")]
    public string? DescriptionKey;

    /// <returns>Localized Description from Description Key.</returns>
    public void GetDescription() => Loc.Get(DescriptionKey);

    #endregion

    /// [De]serialized component entries part of this prototype. Put on end of order.
    [YamlMember(Alias = "components", Order = 99)]
    public List<ComponentYaml>? YamlComponents = [];

    /// <summary>
    /// Reverse-reference back to the world that this entity inhabits.
    /// </summary>
    [MemoryPackIgnore, YamlIgnore, System.Text.Json.Serialization.JsonIgnore]
    public IWorld? World { get; set; }

    #region Constructors & UID

    /// Constructor for flat "empty" Entity. NOT recommended without special handling for Entity's fields.
    [MemoryPackConstructor, System.Text.Json.Serialization.JsonConstructor]
    public Entity() : base()
    {
    }

    [MemoryPackIgnore, YamlIgnore, System.Text.Json.Serialization.JsonIgnore]

    public EntityUid? Uid { get; set; } = null;

    /// Does not permit more than one set. An entity keeps its UID consistently.
    public void SetUID(EntityUid entityUid) => Uid ??= entityUid;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    /// <returns></returns>
    /// <exception cref="NullReferenceException">
    /// A null exception occurs when an entity not "properly" initialized is acted-upon by the Component Registry,
    /// or any system that involves a Uid that may be null.
    /// </exception>
    public static implicit operator EntityUid(Entity entity) => (EntityUid)entity.Uid!;

    #endregion

    /// <summary>
    /// Effective cloning from one source, copied into this entity. Override & add base call at top of function
    /// followed by additional copied-fields for any entity definitions that require their own implementation of
    /// this method.
    /// </summary>
    public virtual Entity CopyFrom(Entity source)
    {
        Source = source.Source;
        Type = source.Type;
        Abstract = source.Abstract;
        Inherit = source.Inherit;
        Handle = source.Handle;
        NameKey = source.NameKey;
        DescriptionKey = source.DescriptionKey;
        World = source.World;

        if (source.YamlComponents != null && YamlComponents != null)
            YamlComponents = YamlComponents
                .Select(c => c.Clone())
                .ToList();
        else
            YamlComponents = [];

        return this;
    }


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
}