using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SKSSL.ECS;

/// System with supported update calls.
public interface IUpdateSystem
{
    void Update(GameTime gameTime);
}

/// System with supported draw calls.
public interface IDrawSystem
{
    GraphicsDevice GraphicsDevice { get; set; }
    void Draw(GameTime gameTime);
}

