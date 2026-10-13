using System;
using System.Text;

namespace SKSSL.Serializing;

/// A block of YAML text data that represents a single entry in a file, which is assumed to be a list of blocks.
public readonly record struct YamlBlock(Type? Type, string Tag, string Text, string File, int Index)
{
    /// Explicit representation of Type in Assembly that this block represents.
    public readonly Type? Type = Type;

    /// Type Tag that the block represents.
    public readonly string Tag = Tag;

    /// Text contained in the block.
    public readonly string Text = Text;

    /// File path to trace back-to in case of errors.
    public readonly string File = File;

    /// Index in the file that which this is defined.
    public readonly int Index = Index;

    /// Convert Text contained in this block to Bytes with [not] provided encoding.
    public byte[] ToBytes(Encoding? encoding = null)
        => encoding == null ? Encoding.UTF8.GetBytes(Text) : encoding.GetBytes(Text);

    /// Returns Block <see cref="Text"/>.
    public override string ToString() => Text;
}