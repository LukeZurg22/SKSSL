namespace SKSSL.ECS;

public interface IEntityEvent;

public interface ICancellableEvent : IEntityEvent
{
    public bool Cancelled { get; set; }

    // ReSharper disable once UnusedMember.Global
    public void Cancel() => Cancelled = true;
}