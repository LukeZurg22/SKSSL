using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;

// ReSharper disable MemberCanBeProtected.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable VirtualMemberNeverOverridden.Global

namespace SKSSL.ECS.Registry;

public interface Registry
{
    public int Count();
    public void Clear();
    public void Register(string handle, object obj);
    public bool Contains(string handle);
    public bool TryGet(string handle, [MaybeNullWhen(false)] out object definition);

    /// Linkage step that permits this registry to interact with other registries.
    public void Link();
}

/// <summary>
/// Specialized registry for handling different prototype definitions within the confines of the ECS and Content Loader.
/// </summary>
public abstract class Registry<T> : Registry where T : class, new()
{
    /// <summary>
    /// Key = Handle, T = Definition Instance. These are non-processed instances, and are Individual definitions
    /// belonging to instances loaded from yaml.
    /// </summary>
    protected readonly Dictionary<string, T> RegistryEntries = [];

    /// <param name="handle">Full Source:Handle ID that the Entity Registry definitions should possess.</param>
    /// <returns>True if a template was found. False if one was not.</returns>
    [Pure]
    public virtual bool Contains(string handle) => RegistryEntries.ContainsKey(handle);

    public virtual int Count() => RegistryEntries.Count;

    public virtual void Clear() => RegistryEntries.Clear();

    /// Do not override this!
    void Registry.Register(string handle, object obj) => Register(handle, (T)obj);

    /// Override me!
    public virtual object? Register(string handle, T entry)
    {
        RegistryEntries[handle] = entry;
        return entry;
    }

    public bool TryGet(string handle, [NotNullWhen(true)] out object? definition)
    {
        bool output = TryGet(handle, out T? thing);
        definition = thing;
        return output;
    }

    //@formatter:off
    public virtual void Link() { /**/ }
    //@formatter:on

    /// <summary>
    /// Safe[r] TryGet method to retrieve a definition using a reference id.
    /// </summary>
    /// <returns>True if was found. False if one was not. The output is also Null if one was not found.</returns>
    public virtual bool TryGet(string handle, [MaybeNullWhen(false)] out T definition)
        => RegistryEntries.TryGetValue(handle, out definition);
}