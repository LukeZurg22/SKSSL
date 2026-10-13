using Microsoft.Xna.Framework;

namespace SKSSL.ECS;

/// System with supported draw calls.
public interface IDrawSystem
{
    void Draw(GameTime gameTime);
}

