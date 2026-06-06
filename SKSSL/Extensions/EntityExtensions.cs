using System.Collections.Generic;
using System.Diagnostics.Contracts;
using SKSSL.ECS;

namespace SKSSL.Extensions;

public static partial class EntityExtensions
{
    /// Gets current Game's EntityContext.
    private static ComponentRegistry ComponentRegistry => SSLGame.Instance.SceneManager.ECS().Components;

    /// Retrieves all entities from active EntityContext.
    [Pure]
    private static IReadOnlyList<Entity> Entities => SSLGame.Instance.SceneManager.ECS().EntityManager.AllEntities;
}
/*
    You there, yes, you! Welcome!
        This class is the parent of Entity Extensions.
            These Entity Extension classes depend upon this one.
                Expand this class within your IDE's navigation to see them.
                    Here lies the self-reflective "Get" methods for the game's EntityContext.

                ┌────────────────────────────────────────────────────────────────────┐
                │                       │EntityExtensions│                           │
                │                                │                                   │
                │                                │                                   │
                ├──────────────────────────┐     │     ┌─────────────────────────────┤
                │EntityExtensions (Queries)◄─────┼────►│EntityExtensions (Components)│
                ├──────────────────────────┘     │     └─────────────────────────────┤
                │                                │                                   │
                │                                │                                   │
                │                   ┌────────────▼───────────┐                       │
                │                   │EntityExtensions (Clone)│                       │
                └───────────────────┴────────────────────────┴───────────────────────┘

            Enjoy,
            -Z
*/