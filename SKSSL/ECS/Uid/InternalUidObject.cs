namespace SKSSL.ECS;

/// <summary>
/// When inherited, exclaims that the implementor must contain an internal Uid.
/// </summary>
public interface InternalUidObject
{
    internal void SetUid(PackableUid uid);
    public PackableUid GetUid();
}