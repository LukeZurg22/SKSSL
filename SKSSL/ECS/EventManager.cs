// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedTypeParameter

using System;
using System.Collections.Generic;

namespace SKSSL.ECS;

/// <summary>
/// Event handler that allows systems to subscribe and raise event calls.
/// </summary>
public class EventHandler
{
    private readonly Dictionary<Type, List<Delegate>> _handlers = new();

    public void Subscribe<T>(Action<EntityUid, T> handler) where T : struct, IEntityEvent
    {
        Type type = typeof(T);
        if (!_handlers.TryGetValue(type, out var list))
        {
            list = [];
            _handlers[type] = list;
        }

        list.Add(handler);
    }

    public void Subscribe<TComp, TEvent>(
        Func<EntityUid, bool> hasComponent, Action<EntityUid, TEvent> handler)
        where TEvent : struct, IEntityEvent =>
        Subscribe<TEvent>((uid, ev) =>
        {
            if (hasComponent(uid)) handler(uid, ev);
        });

    public void Raise<T>(EntityUid entityId, T @event) where T : struct, IEntityEvent
    {
        Type type = typeof(T);

        if (!_handlers.TryGetValue(type, out var list))
            return;

        foreach (Delegate handler in list)
        {
            ((Action<EntityUid, T>)handler)(entityId, @event);

            // STOP if cancelled
            // ReSharper disable once SuspiciousTypeConversion.Global // Suspicious Type Check disabled here as
            //  developer implement for custom events may inherit ICancellableEvent.
            if (@event is ICancellableEvent cancellableEvent && cancellableEvent.Cancelled)
                break;
        }
    }
}