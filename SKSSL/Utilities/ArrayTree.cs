using System;
using System.Runtime.CompilerServices;
using static System.Array;
using static System.Runtime.CompilerServices.RuntimeHelpers;

// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedType.Global

namespace SKSSL.Utilities;

public readonly record struct NodeId(int Index)
{
    public static readonly NodeId None = new(-1);
    public bool IsNone => Index < 0;
    public bool IsValid => Index >= 0;
}

public sealed class ArrayTree<T>(int capacity = 64)
{
    private T[] _values = new T[capacity];

    private NodeId[]
        _parent = new NodeId[capacity],
        _firstChild = new NodeId[capacity],
        _lastChild = new NodeId[capacity],
        _nextSibling = new NodeId[capacity],
        _order = new NodeId[capacity];

    private int _orderCount;
    private bool _orderValid;

    public int Count { get; private set; }

    public NodeId Root => Count > 0 ? new NodeId(0) : NodeId.None;

    public ref T ValueAt(NodeId id) => ref _values[id.Index];
    public NodeId ParentOf(NodeId id) => _parent[id.Index];
    public NodeId FirstChildOf(NodeId id) => _firstChild[id.Index];
    public NodeId NextSiblingOf(NodeId id) => _nextSibling[id.Index];

    /// Override for <see cref="Add(NodeId, T)"/>. Appends to the root.
    public NodeId Add(T value) => Add(NodeId.None, value);

    /// <summary>Add a value to a parent node.</summary>
    /// <param name="parent">Pass NodeId.None as the parent for the root.</param>
    /// <param name="value"></param>
    public NodeId Add(NodeId parent, T value)
    {
        if (Count == _values.Length) 
            Grow();

        var id = new NodeId(Count++);
        _values[id.Index] = value;
        _parent[id.Index] = parent;
        _firstChild[id.Index] = _lastChild[id.Index] = _nextSibling[id.Index] = NodeId.None;

        if (parent.IsValid)
        {
            if (_firstChild[parent.Index].IsNone) _firstChild[parent.Index] = id;
            else _nextSibling[_lastChild[parent.Index].Index] = id;
            _lastChild[parent.Index] = id;
        }

        _orderValid = false;
        return id;
    }

    public void Clear()
    {
        if (IsReferenceOrContainsReferences<T>())
            Array.Clear(_values, 0, Count);

        Count = 0;
        _orderCount = 0;
        _orderValid = false;
    }

    /// Node ids in breadth-first order. Cached until the tree changes.
    public ReadOnlySpan<NodeId> BreadthFirstOrder()
    {
        if (_orderValid)
            return _order.AsSpan(0, _orderCount);

        int head = 0, tail = 0;
        if (Count > 0)
        {
            _order[tail++] = new NodeId(0);
            while (head < tail)
            {
                NodeId node = _order[head++];
                for (NodeId c = _firstChild[node.Index]; c.IsValid; c = _nextSibling[c.Index])
                    _order[tail++] = c;
            }
        }

        _orderCount = tail;
        _orderValid = true;

        return _order.AsSpan(0, _orderCount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Grow()
    {
        int size = _values.Length * 2;
        Resize(ref _values, size);
        Resize(ref _parent, size);
        Resize(ref _firstChild, size);
        Resize(ref _lastChild, size);
        Resize(ref _nextSibling, size);
        Resize(ref _order, size);
    }
}