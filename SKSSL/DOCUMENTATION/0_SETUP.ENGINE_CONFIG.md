# E.C.S. Toggle
Required for default game content handling and certain features. This toggle is the crux of the engine, but can be
disabled if so desired. UseECS is `true` by default.

# GUM

A Gum project file name can be overridden in the Static variable "GumFile", which assumes the file is located in
Content/Gum/`<file name>.gumx`. Change this in your special `Game.cs` class' static constructor, much like
`UsesECS`.
> The way a GUM file is loaded may change!

# Game Content Loader
By default this requires the ECS to be enabled! The GameContentLoader class is an abstract inheritable class that allows a developer to create their own loader that
couples into the system. They are provided abstract [De]Serialize methods to override to turn text into Prototype lists.

The Content Loaders exclusively handle prototyping, nothing more. Supply your own extensions, and your own logic!

    protected override string[] Extensions => [...];
The Load method may also be overwritten, which will cease any Deserialization calls it would normally make. The sky
is the limit! Overriding the Load call will allow one to circumvent the UsesECS check. Interacting with the prototype
registries whilst UsesECS is disabled may cause unexpected behaviour, or **crashes**.
