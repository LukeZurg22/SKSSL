using System.Diagnostics;
using Microsoft.Xna.Framework;
using SKSSL.ECS;
using SKSSL.Extensions;

namespace SKSSL.Tests.TestData;

[RegisterSystem]
public class TestSystem : EntitySystem
{
    public void Update(Entity entity, GameTime gameTime)
    {
        var comp2 = entity.GetComponent<TestFieldComponent>();
        var comps = entity.GetAllComponents();

        entity.AddComponent<TestBlankComponent>();
        
        if (entity.HasComponent<TestFieldComponent>())
            ApplyDamage(entity, 1);
    }

    public void ApplyDamage(Entity ent, int damageAmount)
    {
        if (!ent.HasComponent<TestFieldComponent>())
            return;
        ref TestFieldComponent comp = ref ent.GetComponent<TestFieldComponent>();

        comp.y += damageAmount;

        // Optional: Check for death
        if (comp.y >= 100)
        {
            OnEntityDied(ent);
        }
    }

    private void OnEntityDied(Entity entity)
    {
        Debug.WriteLine("Brochacho, this is pretty sick, yo.");
    }
}