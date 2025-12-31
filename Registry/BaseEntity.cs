using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SKSSL.Localization;
using SKSSL.Utilities;
using SKSSL.YAML;

namespace SKSSL.Registry;

public abstract class BaseEntity
{
    /// <summary>
    /// Static ID of this particular entry.
    /// </summary>
    protected string Id { get; set; }

    #region Localization Properties

    /// <summary>
    /// Localization for name.
    /// </summary>
    protected string Name { get; set; }

    /// <summary>
    /// Localization for description.
    /// </summary>
    protected string Description { get; set; }

    public void GetName() => Loc.Get(Name);
    public void GetDescription() => Loc.Get(Description);

    #endregion

    public BaseEntity(BaseLocalizedYamlEntry yaml)
    {
        Id = yaml.Id;
        Name = yaml.Name;
        Description = yaml.Description;
    }
    
    public Vector3Int WorldPosition { get; set; }
    public Vector3 LocalPosition { get; set; }
    public Vector3 Velocity { get; set; }
    public float Rotation { get; set; }

    protected virtual T Clone<T>() where T : BaseEntity
    {
        // Use MemberwiseClone to create a shallow copy of the current object
        var clone = (T)MemberwiseClone();

        // Re-initialize if needed (may not be needed)
        clone.Initialize();

        // Preserve original ID
        clone.Id = Id;

        return clone;
    }
    
    public virtual void Initialize()
    {
    }
    
    public virtual void Draw(SpriteBatch spriteBatch)
    {
    }

    public virtual void Update(GameTime gameTime)
    {
        
    }
}