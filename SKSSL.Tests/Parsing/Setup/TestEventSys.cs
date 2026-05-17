#nullable enable
using System.Collections.Generic;
using SKSSL.ECS;
using SKSSL.Extensions;
using static SKSSL.DustLogger;

namespace SKSSL.Tests.Tests.Parsing.Setup;
public record DamageEvent(SKEntity Target, int Amount, SKEntity? Source = null);

public class DamageEventSystem
{
    public void ProcessEvents(List<DamageEvent> events)
    {
        foreach (DamageEvent evt in events)
        {
            if (!evt.Target.HasComponent<TestIskComponent>()) continue;
            ref TestIskComponent health = ref evt.Target.GetComponent<TestIskComponent>();
            health.y -= evt.Amount;

            Log($"Entity {evt.Target.RuntimeId} took {evt.Amount} damage from {evt.Source?.RuntimeId ?? -1}");
        }
        events.Clear();
    }
}