using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using MemoryPack;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SKSSL.Localization;
using SKSSL.Scenes;
using SKSSL.YAML;
using VYaml.Annotations;

// ReSharper disable VirtualMemberCallInConstructor
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable VirtualMemberNeverOverridden.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable RedundantBaseConstructorCall
// ReSharper disable ClassNeverInstantiated.Global

namespace SKSSL.ECS;

/// <summary>
/// Instanced Entity representing an object present within game memory. Entities are contained within a
/// <see cref="World"/>, and contain <see cref="ComponentIndices"/> for pointing to component arrays.
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
[YamlObject]
public partial record Entity : Prototype
{
    /// <inheritdoc cref="Prototype.Type"/>
    [YamlMember(name: "type")]
    public override string Type { get; set; } = "Entity";

    /*
     * All Entities are expected to have name and description keys provided. This isn't as limiting as it seems.
     * Inheriting directly from the root Prototype record allows one to create a new prototype definition which does
     * not need to contain a name.
     */

    // TODO: Add Parentage Field, where the properties of the parent are introduced to the child.
    //  Child overrides parent properties. This replaces the templating system of olde.

    #region Name / Description

    /// Non-localized name key.
    [YamlMember(name: "name"), JsonInclude, JsonPropertyName("Name")]
    public string? NameKey;

    /// <returns>Localized name from Name Key.</returns>
    public void GetName() => Loc.Get(NameKey);

    /// Non-localized description key.
    [YamlMember(name: "description"), JsonInclude, JsonPropertyName("Description")]
    public string? DescriptionKey;

    /// <returns>Localized Description from Description Key.</returns>
    public void GetDescription() => Loc.Get(DescriptionKey);

    #endregion

    /// Unique runtime ID. Created on instantiation.
    [YamlIgnore, JsonIgnore] public readonly EntityUid Uid = new();

    /// <returns>true if ID != 0, else returns false.</returns>
    [YamlIgnore, JsonIgnore] public bool IsValid => Uid.Id != 0U;

    /// [De]serialized component entries part of this prototype.
    [YamlMember(name: "components")] public List<YamlComponent>? YamlComponents = [];
    
    /// <summary>
    /// Array of component indices.<br/>
    /// Index = ComponentTypeId&lt;T&gt;.Id,<br/>
    /// Value = slot in ComponentArray&lt;T&gt; (-1 if missing)
    /// <br/><br/>
    /// For every index, there is a unique component type.
    /// <seealso cref="IterArray{T}"/>
    /// </summary>
    [MemoryPackIgnore, YamlIgnore, JsonIgnore]
    public readonly int[] ComponentIndices = null!;

    /// <summary>
    /// Reverse-reference back to the world that this entity inhabits.
    /// </summary>
    [MemoryPackIgnore, YamlIgnore, JsonIgnore]
    public IWorld? World { get; set; }

    #region Constructors

    /// <summary>
    /// Entities may use inherited template types to fill certain details in
    /// their constructors but always MUST call this base constructor. 
    /// </summary>
    public Entity(Prototype prototype) : this()
    {
        Type = prototype.Type;
        Handle = prototype.Handle;
    }

    /// Constructor for flat "empty" Entity. NOT recommended without special handling for Entity's fields.
    [MemoryPackConstructor, JsonConstructor, YamlConstructor]
    public Entity() : base()
    {
        NameKey = "";
        DescriptionKey = "";
        ComponentIndices = new int[ComponentRegistry.Count];
        Array.Fill(ComponentIndices, -1); // <- All slots start as "missing"
    }

    #endregion

    /// <summary>
    /// Special initialization logic for Entity.
    /// </summary>
    /// <remarks>
    /// This is called from <see cref="EntityManager.Spawn(string)"/>; as in on-spawn.
    /// <br/><br/>
    /// Systems will automatically act upon an entity's components, this method is a formality for special
    /// alternative behaviour on-creation.
    /// </remarks>
    public void Initialize()
    {
    }

    /// Special draw instructions per-entity, should a Rendering component not be enough.
    public void Draw(SpriteBatch spriteBatch)
    {
    }

    /// Special entity behaviour / status update call, should behavioural components not be enough.
    public void Update(GameTime gameTime)
    {
    }

    /// Implicit operator to convert an entity into its UID.
    public static implicit operator EntityUid(Entity entity) => entity.Uid;

    public virtual bool Equals(Entity? other) => other is not null && Uid == other.Uid;
    public override int GetHashCode() => Uid.GetHashCode();
}