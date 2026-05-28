// ReSharper disable NotAccessedField.Global

using VYaml.Annotations;

namespace SKSSL.ECS;

#pragma warning disable CS8618, CS9264

/// Required interface to implement custom components.
/// <remarks>
/// SKSSL's ECS uses reflection to get all ISKComponents and their fields.
/// Components exist here solely to store and represent data within an entity.
/// </remarks>
[YamlObject]
public partial record Component
{
    /// <summary>
    /// ID reference back to parent entity this control belongs to.
    /// </summary>
    [YamlIgnore] public EntityUid Entity;
}