using System.Collections.Generic;
using SKSSL.Utilities;

// ReSharper disable UnusedMember.Global

namespace SKSSL.ECS;

public class EntityUid
{
    private static readonly IDIterator _idIterator = new();

    public readonly uint Id;

    // TODO: Reorg. to SoA
    public readonly Entity Entity = null!;
    public EntityUid? Parent = null;
    public List<EntityUid> Children = [];

    /// <summary>
    /// Default constructor initializing Uid to 0 (invalid).
    /// </summary>
    public EntityUid() => Id = 0;

    public EntityUid(Entity entity)
    {
        Id = _idIterator.ID; // Assign current iterator ID by value. (Thread safe.)
        _idIterator.Iterate();
        Entity = entity;
    }

    public static implicit operator uint(EntityUid uid) => uid.Id;
    public static implicit operator Entity(EntityUid uid) => uid.Entity;
}