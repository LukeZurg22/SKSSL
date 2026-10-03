// ReSharper disable RedundantAttributeUsageProperty
// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

using System;

namespace SKSSL;

/// <summary>
/// Marks the class this attribute is tied-to as viable for automatic system registry.
/// World data is provided on-registration.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class RegisterCommandAttribute : Attribute
{
    public RegisterCommandAttribute(string name = "") => Name = name;

    public string? Name { get; }
}