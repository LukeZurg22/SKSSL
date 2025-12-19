using System;
using SKSSL.YAML;

#pragma warning disable CS8618, CS9264

// ReSharper disable UnusedType.Global
// ReSharper disable NotAccessedField.Global
// ReSharper disable UnusedParameter.Global
// ReSharper disable UnusedMember.Global

namespace SKSSL.Registry;

/// <summary>
/// Template component for special mechanics.
/// </summary>
public interface IUpdatableComponent
{
    public void Update(float deltaTime);
}

/// <summary>
/// Specifically used for certain registries that require a specific <see cref="Update"/> call.
/// </summary>
public interface IRegisteryUpdatable
{
    /// <summary>
    /// Called externally by reflection. For unique cases that require a fine touch.
    /// </summary>
    public static abstract void Update(float deltaTime);
}

/// <summary>
/// A basic mechanic that is linked to a parent, ready for updating.
/// <inheritdoc cref="BaseLocalizedYamlEntry"/>
/// </summary>
public record BaseComponent : BaseLocalizedYamlEntry, IUpdatableComponent
{
    /// Localization for mechanic's description.
    
    internal object? Parent = null;

    public virtual void Update(float deltaTime)
    {
    }

    public void SetParent(object parent) => Parent = parent;
    public virtual bool Equals(BaseComponent? other) => other != null && string.Equals(Id, other.Id, StringComparison.Ordinal);
    public bool Equals(string otherId) => string.Equals(Id, otherId, StringComparison.Ordinal);
}

public class Stat
{
    public string Id { get; set; }
}