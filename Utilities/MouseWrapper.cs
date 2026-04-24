using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace SKSSL.Utilities;

/// <summary>
/// A wrapper for general fields and functions to control mouse behaviour. This can be called elsewhere to dynamically
/// change the "affixed" position of the user's mouse, and can even be done for animations. The mouse affixed position
/// is handled in SSLGame.Update
/// </summary>
public class MouseWrapper
{
    /// <summary>
    /// Instance wrapper that permits access outside of the game class, but remains in Game context.
    /// </summary>
    public static MouseWrapper Instance = null!;
    private readonly GraphicsDeviceManager _graphics;
    
    public bool HasAffixedPosition = false;
    public Vector2 AffixedPosition;
    
    /// Gets game preferred buffer width and height.
    public Vector2 GetScreenSize()
        => new(_graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight);
    
    /// Gets game preferred buffer width and height.
    public Vector2 GetScreenCenter() => GetScreenSize() / 2;
    
    public MouseWrapper(GraphicsDeviceManager graphics)
    {
        _graphics = graphics;
        Instance = this;
    }

    public void SetPositionCenter()
    {
        HasAffixedPosition = true;
        AffixedPosition = GetScreenCenter();
    }

    public void SetPosition(float x, float y)
    {
        HasAffixedPosition = true;
        AffixedPosition = new Vector2(x, y);
    }

    /// Lazy toggle for affixed position.
    public void SetPosition() => HasAffixedPosition = false;

    public void SetPosition(Vector2 x)
    {
        HasAffixedPosition = true;
        AffixedPosition = x;
    }

    /// <summary>
    /// Called by SSLGame.<see cref="SSLGame.Update"/> to affix mouse to specific position.
    /// </summary>
    public static void HandleForcedPosition()
    {
        if (Instance.HasAffixedPosition)
            Mouse.SetPosition((int)Instance.AffixedPosition.X, (int)Instance.AffixedPosition.Y);
    }
}