using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SKSSL.Localization;
using SKSSL.Scenes;
using SKSSL.YAML;

// ReSharper disable ClassNeverInstantiated.Global

namespace SKSSL.ECS;

public record SKEntity
{
    #region IDs

    /// <summary>
    /// Static ID of this particular entry to a template reference.
    /// </summary>
    internal string ReferenceId { get; init; } = null!;

    /// <summary>
    /// Unique runtime ID (only set on spawned instances, -1 on templates)
    /// </summary>
    public int RuntimeId { get; } = -1;

    /// <summary>
    /// Defers back to the <see cref="RuntimeId"/> for compatability reasons between projects.
    /// </summary>
    public int Id => RuntimeId;

    #endregion
    
    /// <summary>
    /// Array of component indices. Index = ComponentTypeId&lt;T&gt;.Id, Value = slot in ComponentArray&lt;T&gt; (-1 if missing)
    /// <br/><br/>
    /// For every index, there is a unique component type.
    /// <seealso cref="ComponentArray{T}"/>
    /// </summary>
    public readonly int[] ComponentIndices;

    /// <summary>
    /// Reverse-reference back to the world that this entity inhabits.
    /// </summary>
    public BaseWorld? World;
    
    #region Localization Properties

    /// <summary>
    /// Localization for name.
    /// </summary>
    internal string NameKey { get; set; } = null!;

    /// <summary>
    /// Localization for description.
    /// </summary>
    internal string DescriptionKey { get; set; } = null!;

    public void GetName() => Loc.Get(NameKey);
    public void GetDescription() => Loc.Get(DescriptionKey);

    #endregion

    /// <summary>
    /// Default required constructor. Inheritance-entities may use inherited template types to fill certain details in
    /// their constructors and but always MUST call this base constructor. 
    /// </summary>
    /// <param name="id">Unique numerical of of the entity.</param>
    /// <param name="count">Number of component indices in the game.</param>
    /// <param name="template">Provided template. Uses base <see cref="EntityTemplate"/> by default.</param>
    protected SKEntity(int id, int count, EntityTemplate template)
    {
        ReferenceId = template.ReferenceId;
        NameKey = template.NameKey;
        DescriptionKey = template.DescriptionKey;
        
        RuntimeId = id;
        ComponentIndices = new int[count];
        Array.Fill(ComponentIndices, -1); // ← All slots start as "missing"
    }

    public void Initialize()
    {
    }

    public void Draw(SpriteBatch spriteBatch)
    {
    }

    public void Update(GameTime gameTime)
    {
    }
}