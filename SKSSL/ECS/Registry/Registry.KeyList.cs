using System.Collections.Generic;
using SKSSL.Serializing;

// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global

namespace SKSSL.ECS.Registry;

/// <remarks>
/// Rather than permit KeyLists to the realm of the Raw Prototypes registry, they are allocated here.
/// This works 1:1 with the raw registry with the added benefit of being isolated into their own, and with
/// special handling for localized key-list lists.
/// </remarks>
public class KeyListRegistry : Registry<KeyList>
{
    /// <summary>
    /// Works like the typical TryGet for the prototype entry, but calls
    /// <see cref="KeyList.ValuesAsResolved"/>.
    /// </summary>
    /// <param name="handle"></param>
    /// <param name="localizations"></param>
    /// <returns></returns>
    public bool TryGetLocalized(string handle, out IEnumerable<LocKey> localizations)
    {
        localizations = [];
        bool result = TryGet(handle, out KeyList? prototype);
        if (prototype != null)
            localizations = prototype.Values;
        return result;
    }
}