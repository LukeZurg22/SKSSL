using SKSSL.YAML;

// ReSharper disable ClassWithVirtualMembersNeverInherited.Global

#pragma warning disable CS8618, CS9264

namespace SKSSL.Registry;

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

public record BaseRegisterable : BaseLocalizedYamlEntry
{
    public string Id { get; set; }

    public virtual void Update(float deltaTime)
    {
    }
}