using System;
using System.Collections;
using System.Collections.Generic;
using YamlDotNet.Serialization;
using static YamlDotNet.Serialization.DefaultValuesHandling;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedAutoPropertyAccessor.Local
// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local

namespace SKSSL.ECS.DataSet;

public class KeyListPrototype : Prototype
{
    // ->+ type
    public override string Type { get; set; } = "KeyList";

    // ->+ handle
    // public string Handle (aka ID)

    /// <example>
    /// <code>
    /// - type: KeyList
    ///   id: MyKeyList
    ///   values:
    ///     prefix: localization-entry-
    ///     count: 99
    /// or
    /// - type: KeyList
    ///   id: MyKeyList
    ///   values:
    ///     keys: # Declaring keys makes them explicit. Prefix and count are ignored!
    ///      - localization-entry-0
    ///         ...
    ///      - localization-entry-#
    ///      - other-localization-entry-#
    /// </code>
    /// </example>
    public KeyListValues Values;

    /// <returns>Values Key List, but as a localized set of values as its keys.</returns>
    // Localization WILL complain if any are explicit but not present.
    public IEnumerable<string> GetValuesAsLocalized()
    {
        foreach (string keyListValue in Values)
            yield return Loc.Get(keyListValue);
    }
}

[YamlSerializable]
public sealed partial class KeyListValues : IReadOnlyList<string>
{
    /// <summary>
    /// Expected prefix to key list.
    /// </summary>
    [YamlMember(DefaultValuesHandling = OmitDefaults)]
    public string Prefix { get; private set; } = default!;

    /// <summary>
    /// Expected number of entries in the list.
    /// </summary>
    [YamlMember]
    public int Count { get; private set; }

    /// Explicit override keys that can be overwritten.
    [YamlMember(DefaultValuesHandling = OmitNull | OmitEmptyCollections)]
    public string[]? Keys { get; private set; }

    /// <returns>true if keys are explicitly declared, and not empty; else false</returns>
    public bool AreKeysExplicit => Keys != null && Keys.Length > 0;

    /// <summary>
    /// Get string value of [prefix+index] provided, assuming keys are not explicit. Explicit key indexing
    /// means the index is used through the keys list.
    /// </summary>
    /// <param name="index">Index of key in list, or number to append if non-explicit list.</param>
    /// <exception cref="IndexOutOfRangeException">
    /// Thrown when an index is out of bounds of either the
    /// keys list, or a provided entry-count.
    /// </exception>
    public string this[int index]
    {
        get
        {
            if (AreKeysExplicit)
            {
                if (index < 0 || index >= Keys!.Length)
                    throw new IndexOutOfRangeException();
                return Keys[index];
            }

            if (index < 0 || index >= Count)
                throw new IndexOutOfRangeException();

            return Prefix + (index + 1);
        }
    }

    /// <returns>Number of keys if they are provided, or the expected key count if not.</returns>
    public int Length => AreKeysExplicit ? Keys!.Length : Count;

    /// <inheritdoc cref="Length"/>
    int IReadOnlyCollection<string>.Count => Length;

    public IEnumerator<string> GetEnumerator()
        => AreKeysExplicit
            ? ((IEnumerable<string>)Keys!).GetEnumerator()
            : new Enumerator(this);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public sealed class Enumerator : IEnumerator<string>
    {
        private int _index = 0;
        private readonly KeyListValues _values;

        public Enumerator(KeyListValues values) => _values = values;

        public string Current => _values.Prefix + _index;
        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            _index++;
            return _index <= _values.Count;
        }

        public void Reset() => _index = 0;

        public void Dispose()
        {
        }
    }
}