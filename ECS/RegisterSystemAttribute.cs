// ReSharper disable RedundantAttributeUsageProperty

namespace SKSSL.ECS;

/// <summary>
/// Marks the class this attribute is tied-to as viable for automatic system registry.
/// World data is provided on-registration.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class RegisterSystemAttribute : Attribute
{
    /// To control order or phase of system.
    public int Order { get; set; } = 0;
}