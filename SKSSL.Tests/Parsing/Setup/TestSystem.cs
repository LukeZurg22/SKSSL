using System.Diagnostics;
using Microsoft.Xna.Framework;
using SKSSL.ECS;
using SKSSL.Extensions;

namespace SKSSL.Tests.Tests.Parsing.Setup;

[RegisterSystem]
public class TestSystem : EntitySystem
{
    public void Update(SKEntity entity, GameTime gameTime)
    {
        var comp2 = entity.GetComponent<TestIskComponent>();
        var comps = entity.GetAllComponents();

        entity.AddComponent<PositionIskComponent>();
        
        if (entity.HasComponent<TestIskComponent>())
            ApplyDamage(entity, 1);
    }

    public void ApplyDamage(SKEntity ent, int damageAmount)
    {
        if (!ent.HasComponent<TestIskComponent>())
            return;
        ref TestIskComponent comp = ref ent.GetComponent<TestIskComponent>();

        comp.y += damageAmount;

        // Optional: Check for death
        if (comp.y >= 100)
        {
            OnEntityDied(ent);
        }
    }

    private void OnEntityDied(SKEntity entity)
    {
        Debug.WriteLine("Brochacho, this is pretty sick, yo.");
    }
}