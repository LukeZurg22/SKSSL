using System.Collections.Generic;
using SKSSL.ECS.DataSet;

namespace SKSSL.ECS;

/// <remarks>
/// Rather than permit KeyLists to the realm of the Raw Prototypes registry, they are allocated here.
/// This works 1:1 with the raw registry with the added benefit of being isolated into their own, and with
/// special handling for localized key-list lists.
/// </remarks>
public class ECSRegistry_KeyList : ECSRegistry<KeyListPrototype>
{
    /// <summary>
    /// Works like the typical TryGet for the prototype entry, but calls
    /// <see cref="KeyListPrototype.GetValuesAsLocalized"/>.
    /// </summary>
    /// <param name="handle"></param>
    /// <param name="localizations"></param>
    /// <returns></returns>
    public bool TryGetAsLocalizedList(string handle, out IEnumerable<string> localizations)
    {
        localizations = [];
        if (!TryGet(handle, out KeyListPrototype? prototype))
            return false;

        // WIP: Test this! Test test test!
        localizations = prototype.GetValuesAsLocalized();
        return true;
    }
}