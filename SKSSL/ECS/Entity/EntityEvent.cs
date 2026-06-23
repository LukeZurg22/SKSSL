// ReSharper disable UnusedMember.Global

namespace SKSSL.ECS;

public class EntityEvent;

public class CancellableEvent : EntityEvent
{
    public bool Cancelled { get; private set; }
    public void Cancel() => Cancelled = true;

    public CancellableEvent()
    {
    }

    public CancellableEvent(bool cancelled)
    {
        Cancelled = cancelled;
    }
}