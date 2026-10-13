Here, you are provided pre-made serializers. Notably for .json, and .yaml format.

An ISerializer is fed as a generic parameter to the PrototypeLoader, but can be used elsewhere if need-be.

However, file extensions still need to be provided as the PrototypeLoader is still a GameLoader, which expects
those extensions.

```csharp
PrototypeLoader<YamlSerializerSolKom> _prototypeLoader = new(".yaml", ".yml");
```

Creating a serializer by creating a new class inheriting from ISerializer will allow one to use your own serialization
handling elsewhere in a controlled environment. The provided ones use existing libraries, but the way it's handled
permits one the space to write their own if they are so inclined.

The serializer used in the Prototype Loader is changed in the Engine Config.