# Table of Contents
<!-- TOC -->
* [Table of Contents](#table-of-contents)
* [Usage](#usage)
  * [Textures](#textures)
  * [Prototypes](#prototypes)
  * [Localization](#localization)
* [Developing With an ECS](#developing-with-an-ecs)
<!-- TOC -->

---

# Usage
Actually using the engine will depend on which parts are utilized. This is general-purpose, and so certain sections can
be used freely without the others. However, certain parts such as _Prototypes_ require the ECS to be enabled!

Additionally, the game expects `textures`, `prototypes`, `localization`, etc. folders defined in its loaded directories.
If these are not provided, then expect certain parts of the engine to be rendered non-functional!

## Textures
Textures are stored within a textures directory kept within either the root of the game (just outside of the binaries) or
within the root of a provided game-path according to your chosen settings and layout.

Folders inside this directory must be named with one of the following extensions:
1. `<name>.m`<br/>
_Contains a material split into `Diffuse`, `Normal`, `Displacement` and `Emissive` maps. Each file must begin with
the name of the folder that holds them. Otherwise, the loader may exhibit unexpected behaviour._
2. `<name>.i`<br/>
_Contains individual image files, which can be any name._
3. `<name>.t`<br/>
_Contains a tilemap._ _**[Work In Progress]**_

## Prototypes
All prototype files are declared in `.yaml`/`.yml`, `.json`, or whatever file extension(s) your chosen GameContentLoader
supports.

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
