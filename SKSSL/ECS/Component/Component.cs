using MemoryPack;
using YamlDotNet.Serialization;

// ReSharper disable NotAccessedField.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable UnusedMember.Global

namespace SKSSL.ECS;

#pragma warning disable CS8618, CS9264

/// Required "interface" to implement custom components.
/// <remarks>
/// SKSSL's ECS uses reflection to get all ISKComponents and their fields.
/// Components exist here solely to store and represent data within an entity.
/// </remarks>
public partial record Component
{
    [YamlIgnore] internal EntityManager? EntityManager;

    /// ID reference back to parent entity this control belongs to.
    [YamlIgnore] public EntityUid Entity;

    /*     User Input
                ↓
          Game Systems
                ↓
             Physics
                ↓
  IsDirty Synchronized System(s)
                ↓
            Networking
                ↓
           Clear Dirty      */
    [MemoryPackIgnore, YamlIgnore, System.Text.Json.Serialization.JsonIgnore]
    public bool IsDirty { get; private set; }

    public void Dirty()
    {
        if (IsDirty)
            return;

        IsDirty = true;
        EntityManager?.DirtyEntity(Entity);
    }

    internal void ClearDirty() => IsDirty = false;
}