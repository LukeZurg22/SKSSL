#nullable enable
using System.Collections.Generic;
using SKSSL.ECS;
using SKSSL.Extensions;
using static SKSSL.DustLogger;

namespace SKSSL.Tests.TestData;
public record DamageEvent(Entity Target, int Amount, Entity? Source = null);

public class DamageEventSystem
{
    public void ProcessEvents(List<DamageEvent> events)
    {
        foreach (DamageEvent evt in events)
        {
            if (!evt.Target.HasComponent<TestFieldComponent>()) continue;
            ref TestFieldComponent health = ref evt.Target.GetComponent<TestFieldComponent>();
            health.y -= evt.Amount;

            Log($"Entity {evt.Target.Uid} took {evt.Amount} damage from {evt.Source?.Uid}");
        }
        events.Clear();
    }
}