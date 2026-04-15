namespace SKSSL.ECS;

public class EntityEvent;

public class CancellableEvent : EntityEvent
{
    public bool Cancelled;
}