namespace SKSSL.ECS;

/// <summary>
///  "Default" registry if something can't be found. Generic.
/// </summary>
public sealed class ECSRegistry_RawPrototype : ECSRegistry<Prototype>
{
    public static readonly ECSRegistry_RawPrototype Instance = new();
}