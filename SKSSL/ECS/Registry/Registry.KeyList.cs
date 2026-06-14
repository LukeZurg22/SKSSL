using System.Collections.Generic;

// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global

namespace SKSSL.ECS.Registry;

/// <remarks>
/// Rather than permit KeyLists to the realm of the Raw Prototypes registry, they are allocated here.
/// This works 1:1 with the raw registry with the added benefit of being isolated into their own, and with
/// special handling for localized key-list lists.
/// </remarks>
public class KeyListRegistry : Registry<KeyListPrototype>
{
    /// <summary>
    /// Works like the typical TryGet for the prototype entry, but calls
    /// <see cref="KeyListPrototype.ValuesAsLocalized"/>.
    /// </summary>
    /// <param name="handle"></param>
    /// <param name="localizations"></param>
    /// <returns></returns>
    public bool TryGetLocalized(string handle, out IEnumerable<string> localizations)
    {
        localizations = [];
        bool result = TryGet(handle, out KeyListPrototype? prototype);
        if (prototype != null)
            localizations = prototype.ValuesAsLocalized();
        return result;
    }
}