// ReSharper disable RedundantAttributeUsageProperty
// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

using System;

namespace SKSSL.ECS;

/// <summary>
/// Marks the class this attribute is tied-to as viable for automatic system registry.
/// World data is provided on-registration.
/// </summary>
[AttributeUsage(AttributeTargets.Class, /*Inherited = false, (I am allowing inheritance. -Z) */AllowMultiple = false)]
public sealed class RegisterSystemAttribute : Attribute
{
    #region Constructors

    public RegisterSystemAttribute() : this(0)
    {
    }

    public RegisterSystemAttribute(int order) => Order = order;
    public RegisterSystemAttribute(string name) => Name = name;
    public RegisterSystemAttribute(string name, int order) : this(order) => Name = name;

    #endregion

    /// Dedicated name for system.
    public string? Name { get; }

    /// To control order or phase of system. Inverse Order where lower values are handled first, and higher values
    /// are handled last.
    public int Order { get; }
}