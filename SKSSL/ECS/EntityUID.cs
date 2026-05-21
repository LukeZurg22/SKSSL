using System.Collections.Generic;
using SKSSL.Utilities;

// ReSharper disable UnusedMember.Global

namespace SKSSL.ECS;

public class EntityUid
{
    private static readonly IDIterator _idIterator = new();

    public readonly int Id;

    // TODO: Reorg. to SoA
    public readonly Entity Entity = null!;
    public EntityUid? Parent = null;
    public List<EntityUid> Children = [];

    private EntityUid()
    {
        // Assign current iterator ID then iterate.
        Id = _idIterator.ID;
        _idIterator.Iterate();
    }

    public EntityUid(Entity entity) : this()
    {
        Entity = entity;
    }

    public static implicit operator int(EntityUid uid) => uid.Id;
    public static implicit operator Entity(EntityUid uid) => uid.Entity;
}