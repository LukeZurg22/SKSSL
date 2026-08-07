namespace SKSSL.Extensions;

public static partial class EntityExtensions;

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