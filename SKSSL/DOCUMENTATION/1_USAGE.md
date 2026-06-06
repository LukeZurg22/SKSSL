# Usage
Actually using the engine will depend on which parts are utilized. This is general-purpose, and so certain sections can
be used freely without the others. However, certain parts such as _Prototypes_ require the ECS to be enabled!

Additionally, the game expects `textures`, `prototypes`, `localization`, etc. folders defined in its loaded directories.
If these are not provided, then expect certain parts of the engine to be rendered non-functional!

## Textures
THIS IS A WORK IN PROGRESS!

## Prototypes
All prototype files are declared in `.yml` files.

## Localization
All localization files are declared as `.ftl` files, written based on Fluent.NET's handling of the type. All locale
files are under language culture directories (I.e. `en-US`, `de-GE`, etc.) that declare key-value localization pairs
which are loaded at runtime. `Loc.Get` is all that's needed to attempt to get localization for a key, and if a value
is not found then the key is returned.

# Developing With an ECS
To use the ECS, ensure that it is enabled from your `Game.cs` static constructor, where `UsesECS` is set to true!

- All Components are records inheriting the base "Component" record.
- All system classes must implement the [RegisterSystem] attribute.
- All custom prototype- types are records inheriting the base "Prototype" record.
- Toggle SSLGame.UsesECS to true in a static constructor to use ECS.