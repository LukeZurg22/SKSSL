// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedTypeParameter

namespace SKSSL.ECS;

/// <summary>
/// Event handler that allows systems to subscribe and raise event calls.
/// </summary>
public class EventHandler
{
    private readonly Dictionary<Type, List<Delegate>> _handlers = new();

    public void Subscribe<T>(Action<int, T> handler) where T : EntityEvent
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
        Func<int, bool> hasComponent, Action<int, TEvent> handler)
        where TEvent : EntityEvent
    {
        Subscribe<TEvent>((uid, ev) =>
        {
            if (hasComponent(uid)) handler(uid, ev);
        });
    }

    public void Raise<T>(int entityId, T @event) where T : EntityEvent
    {
        Type type = typeof(T);

        if (!_handlers.TryGetValue(type, out var list))
            return;

        foreach (Delegate handler in list)
        {
            ((Action<int, T>)handler)(entityId, @event);
            
            // STOP if cancelled
            if (@event is CancellableEvent cancellableEvent && cancellableEvent.Cancelled)
                break;
        }
    }
}