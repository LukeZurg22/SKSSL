namespace SKSSL.ECS;

public static partial class SystemRegistry
{
    /// Systems merged with generated source Systems file.
    public static IReadOnlyList<EntitySystem> AllSystems { get; } = Array.Empty<EntitySystem>();
    
    /// Helper method to loop over systems.
    public static void ForEach(Action<EntitySystem> action)
    {
        foreach (EntitySystem system in AllSystems)
            action(system);
    }
}