using Microsoft.Xna.Framework;

namespace SKSSL.ECS;

/// System with supported update calls.
public interface IUpdateSystem
{
    void Update(GameTime gameTime);
}