namespace SKSSL.ECS;

/// <summary>
/// When inherited, exclaims that the implementor must contain an internal Uid.
/// </summary>
public interface InternalUidObject
{
    public PackableUid Uid { get; }
}