using System.Text.Json.Serialization;
using MemoryPack;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SKSSL.Scenes;
using SKSSL.YAML;
using VYaml.Annotations;

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
    /// <summary>
    /// Can be overwritten to allow for safe type-casting.
    /// </summary>
    [YamlIgnore, JsonIgnore]
    public Type EntityType => typeof(Entity);

    /// <inheritdoc cref="Prototype.Type"/>
    public override string Type { get; set; } = "entity";

    #region Fields

    /// <summary>
    /// Unique runtime ID (only set on spawned instances, -1 on templates)
    /// </summary>
    [YamlIgnore, JsonIgnore]
    public int RuntimeId { get; private set; } = -1;

    /// Defers back to the <see cref="RuntimeId"/> for compatability reasons between projects.
    [MemoryPackIgnore, YamlIgnore, JsonIgnore]
    public int Id => RuntimeId;

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

    /// Predefined class-specific dictionary of components.
    [MemoryPackIgnore, YamlIgnore, JsonIgnore]
    public IReadOnlyDictionary<Type, object> DefaultComponents { get; init; }

    #endregion

    /// Manually assign runtime ID for if entity is created manually.
    /// Should NOT be called outside of <see cref="EntityManager"/>.
    internal void SetRuntimeId(int id) => RuntimeId = id;

    #region Constructors

    /// <summary>
    /// Entities may use inherited template types to fill certain details in
    /// their constructors but always MUST call this base constructor. 
    /// </summary>
    public Entity(Prototype prototype) : this()
    {
        Handle = prototype.Handle;
        NameKey = prototype.NameKey;
        DescriptionKey = prototype.DescriptionKey;
    }

    /// Construct raw definition using YAML and Default Components.
    internal Entity(Prototype yaml, IReadOnlyDictionary<Type, object> components) : this(yaml)
    {
        DefaultComponents = components;
    }

    internal Entity(int id) : this()
    {
        // For raw definitions, which do not have runtime IDs.
        if (id != -1) SetRuntimeId(id);
    }

    /// Constructor for flat "empty" Entity. NOT recommended without special handling for Entity's fields.
    [MemoryPackConstructor, JsonConstructor, YamlConstructor]
    internal Entity() : base()
    {
        DefaultComponents = new Dictionary<Type, object>();
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
}