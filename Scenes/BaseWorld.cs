using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.Xna.Framework;
using SKSSL.ECS;
using SKSSL.Managers;
using SKSSL.Registry;

namespace SKSSL.Scenes;

public abstract class BaseWorld
{
    public readonly EntityManager _entityManager;
    public List<SKEntity> ActiveEntities => _entityManager.AllEntities.ToList();
    public SKEntity SpawnEntity(string referenceId) => _entityManager.Spawn(referenceId, this);

    private readonly SystemManager _systemManager;

    public BaseWorld()
    {
        _entityManager = new EntityManager();
        _systemManager = new SystemManager();
        
    }

    /// <summary>
    /// Called by <see cref="BaseScene"/> initialization.
    /// <seealso cref="SceneManager"/>
    /// </summary>
    public void Initialize()
    {
        _systemManager.RegisterAll(this);
    }

    public void Update(GameTime gameTime)
    {
        _systemManager.Update(gameTime);
    }

    public void Draw(GameTime gameTime)
    {
        _systemManager.Draw(gameTime);
    }

    /// <summary>
    /// Ensures that this world instance is safely deleted before being replaced.
    /// </summary>
    public void Destroy()
    {
        _entityManager.WipeAllEntities();
    }
}