namespace SKSSL.ECS.Registry;

/// <summary>
///  "Default" registry if something can't be found. Generic.
/// </summary>
public sealed class RawPrototypeRegistry : Registry<Prototype>
{
    public static readonly RawPrototypeRegistry Instance = new();
}