using Newtonsoft.Json;
using YamlDotNet.Serialization;

// ReSharper disable UnusedAutoPropertyAccessor.Global

// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global

namespace SKSSL.ECS;

/// <summary>
/// Component storing object mesh references.
/// </summary>
/// <remarks>Best for use in 3D Projects only!</remarks>
public record MeshComponent : Component
{
    /// Reference to mesh data.
    [YamlMember(Alias = "mesh")] public string MeshHandle;

    // TODO: Add integer-centric mesh handling. Requires a Mesh Loader, first.
    
    /// Reference to material applied to the mesh.
    [YamlMember(Alias = "material")] public int MaterialHandle;

    /// An integer ID handle to assign a numerical ID for faster O(1) lookup.
    /// <remarks>
    /// Only works if the implemented systems accommodate this, and that the handle is stored in an indexable array.
    /// </remarks>
    [YamlIgnore, JsonIgnore]
    public int HandleId { get; private set; } = -1;

    /// Called to assign numeric ID at runtime for easier lookup.
    public void SetMaterialHandleNumericId(int id) => HandleId = id;

    /// Draw layer of the mesh.
    [YamlMember(Alias = "order")] public int Order = 0;

    /// Determines whether this entity is visible.
    [YamlMember(Alias = "visible")] public bool IsVisible = true;
}