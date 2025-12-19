using System.Collections.Generic;
using Gum.DataTypes;
using Gum.Forms.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SKSSL.Scenes;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace SKSSL;

public abstract class BaseGameScene
{
    protected internal Microsoft.Xna.Framework.Game _game;
    internal SpriteBatch _spriteBatch;
    protected internal GraphicsDeviceManager _graphicsManager;
    internal GumProjectSave _gumProjectSave;

    protected readonly List<FrameworkElement> _Menus = [];
    
    public void Initialize(
        Microsoft.Xna.Framework.Game game,
        GraphicsDeviceManager graphicsManager,
        SpriteBatch spriteBatch,
        GumProjectSave gumProjectSave)
    {
        _game = game;
        _graphicsManager = graphicsManager;
        _spriteBatch = spriteBatch;
        _gumProjectSave = gumProjectSave;
    }

    protected abstract void LoadScreens();
    protected abstract void UniqueLoadContent();
    public virtual void LoadContent()
    {
        LoadScreens();
        UniqueLoadContent();
    }

    public void UnloadContent()
    {
        ISceneManager.ClearScreens();

        /*foreach (FrameworkElement screen in _Menus)
        {
            screen.RemoveFromRoot();
        }
        _Menus.Clear();*/
        
        UniqueUnloadContent();
    }
    protected abstract void UniqueUnloadContent();

    public virtual void Update(GameTime gameTime) { }

    public virtual void Draw(GameTime gameTime, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
    {
        
    }
}