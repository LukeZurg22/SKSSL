# What is an ECS?
ECS is short for "Entity Component System", where a set of components contain certain data about the entity they are contained in.

Every component definition *should* have an `EntitySystem` dedicated to it with the current arrangement.

Every `EntitySystem` interacts with as many types of `Components` that you wish and dictates how behaviors are applied to the entities containing those components, but I suggest keeping them 1:1 for the safe of cleanliness.

In essence, what you are seeing here is a `n:m` "Pure" ECS where `n` components could be accessed by `m` systems. If `0` components are accessed by `m` systems, all `m` systems would literally just be plain classes.

```
Example:
    A tree can have the "Grow" component.
    The corresponding "GrowSystem" handles all GrowComponent
        instances, and makes them grow upon the Update() call.
```
Basically, `Entities` hold `Components` which are updated by `Systems`.  It's a very versatile, and performant system that has a lot of potential if used correctly.

# Initializing the ECS
In order to use the ECS provided, one
first must initialize the Component Registry with:
`ComponentRegistry.RegisterAllComponents();`

This should be done on the creation of the game, within the constructor and NOT within Initialize().

# Loading YAML Data
Everything is YAML-centric here. The YAML parser is used extensively, and the ECS provided assumes that
programmer-defined Entity Templates will include component references.

All components in their YAML form are formatted as such:
```yaml
...
components:
  - type: <ComponentName>
    field_a: <?>
    field_b: <?>
    field_c: <?>
    ...
    field_n: <?>
```
Where the component name must equal `ComponentNameComponent` defined in code.
Components must be manually defined by the programmer, and inherit `ISKComponent`.
There can be 'n' number of fields to a provided component, assuming it has the provided fields.

Make sure the fields are named correctly! Every Yaml entry should include an `id` to reference, which is later referred-to as a `reference id`. This id must only be unique to the type of entity defined rather than per-instance, which is provided a numerical ID later when `Entity` creation is involved.

## How To Load Yaml
Simply use either of the provided Bulk or Standard Yaml loaders.
You can use them manually, or implement your own. Currently the system is designed to go through the `GameLoader` by calling:
`GameLoader.Register("Prototypes", "prototypes", GameContentFactory.Load);` in the `Initialize()` method of your Game class inheriting `SSLGame`.

After registering and initializing everything, calling:
`GameLoader.Load(path => GameLoader.GPath(path));` in your game's `LoadContent()` call will load the game loader.

### Note About System Independency
The system, again, is designed to be used by itself. This ECS may provide components and compartmentalization, but some SKSSL systems interact with other SKSSL systems. It's possible to ignore the Game Loader *almost* entirely. There is still a default call in `Initialize()` but if ignored, it should[?] be fine.

## Entity Templating
Once you've acquired your Yaml data one way or another, you'll need to use the `EntityTemplate` system by first registering your Yaml Types through the `EntityRegistry`. This acts as an intermediate step where you can do additional loading and handling for these templates. Templates are (naturally) not instantiated as entities, but instead act as points of reference to copy into your actual `Entity` definitions. While entities *could* be cloned, `EntityTemplate`s **MUST** be cloned to define new entity types.

Start with `RegisterTemplate()` calls to the EntityRegistry. Register all your entities you want to define in Yaml. Just remember that special objects— such as Voxels... :) —are *not* cooperative nor performant with this system. This works best for things such as Mob entities and Items.

Once a template is registered, you can now use the `EntityManager` to instantiate an entity using the template. Naturally, you may want to define your own entity types, which should be inherited from `SKEntity`. Custom Entities can also have custom `EntityTemplate` types provided through their constructors, which allows special handling of data per-template!

To instantiate an entity, utilize the `EntityManager`'s `Spawn()` method. Provide a `reference ID` and optionally the world that which contains the `Entity`.

The method to spawn an entity returns the entity reference, which could be manipulated further.

## About Systems
Spawning `Entities` isn't hard, nor is defining `Components`. However `Systems` that act upon those components need to enact queries. In order to make things clean, a `World` possess a list of `Entity Systems` which reference that world back for the sake of querying a list of `Entities` active in that world. It's one of the cleanest ways this can be done on a per-world basis without forcing a static entities list.
```
Ignoring performance for a moment;
this was done in order to allow networking
and multiple worlds at the same time.

Each world instance has its own entities
and relevant systems.

To get around this and make things easier at
the cost of creating a limitation, 
you may use a static World instance instead.
The system will accomodate you, and a static instance
means easy calls.

Again, this is an optional limitation on your part.
```
Every `EntitySystem` possess an inherited reference to the world it's a part of. To get all components in that world, call `Query<ComponentType>()` on that world reference.

A `Query<>()` call will typically return an IEnumerable of all the entities and/or components you're querying for. It's on you to handle and manipulate them how you see fit.

# Closing Remarks
The arrangement presented to you now currently supports `IUpdatable` and `IDrawable` components.

As of 20250101 `Query` calls only return entities. Additionally, event subscriptions such as `OnInitialize` or `PlayerJumped` aren't supported at the moment. The ECS in its current state is very basic, but *should* get the job done.