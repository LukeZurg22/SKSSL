# SKSSL ![SKSSL Icon|This logo comes from a Victoria 2 Divergences of Darkness came I ran with friends. I was Arcadia. Me, South America, and Spain conquered all of china. This image is a cleaned icon of a screenshot from that game. My country entered a world war and promptly lost Alaska to the goddamned Ottoman Empire player.](..\SKSSL\Assets\icon.png)
This is a shared library turned game engine. "SKSSL" stands for "SolKom Shared Standard Library" This was written in and for C#, and for use with the MonoGame and Gum UI frameworks. Many parts of this library— including its name —are derived from various other fragmented projects of mine whose parts were merged into this. (Example: DustLogger ➡ DustToDust, SolKom ➡ SolarKommand, YAMLParser ➡ XMP, etc.)

## System Information
- .NET 9.0.0 SDK
- Developer OS: Windows 10
- Intended OS('s): Windows & Linux (MacOS Untested.)
- Framework: Monogame DesktopGL 3.4.2+
- Language(s): CS, YAML / YML, FTL

## Program Goal
The goal is to establish a library built on top of Monogame and Gum that acts as a general framework for my other projects. 
I am sharing my active understanding of code as the library progresses, and as I hone my skills.

## Limitations
Given that this is a library, it has limitations from C# version 9+ and its other dependencies. The classes are meant to be as ubiquitous as can be, but very-distant dependency versions past the ones this was developed with may cause issues. Otherwise, the limitations are systematic to the library and anything self-referential within it. (E.g. ECS & BaseWorld, SceneManager & BaseWorld, etc.)

## Maintainer's Remarks
This is all a work-in-progress, but don't wait for releases! When I find a system in this library satisfactory enough for my other projects, I stop adding to that system. Contributions to systems no longer actively maintained are welcome.

## Additional Documentation
1. [Engine Setup](DOCUMENTATION/0_SETUP.md)
   1. [Adjusting The .csproj Project File](DOCUMENTATION/0_SETUP.md#csproj-adjustments)
   2. (Optional) [Attaching GUM UI](DOCUMENTATION/0_SETUP.md#gum)
   3. [Configuring Game Paths](DOCUMENTATION/0_SETUP.md#settings)<br/>
   _Ensure that any pre-defined folders align with the ones mentioned in the `.csproj`!_
   4. [Creating an SKSSL Game Class](DOCUMENTATION/0_SETUP.md#sslgame)
2. [Using SKSSL](DOCUMENTATION/1_USAGE.md)

---

## Licensing and References
This library is explicitly for [Monogame](https://github.com/MonoGame/MonoGame) Projects that
best accomodate the [Gum](https://github.com/vchelaru/Gum?tab=MIT-1-ov-file) Framework for general Menu handling.

This library was made by LukeZurg22, All Rights are Reserved. Under the current license you are permitted to fork and develop it under the conditions that you both accredit me, and maintain your fork's public status. This license may change with time.

### References
Below is a non-exhaustive list of references the project uses, coupled with the uses for these references.

- "FlatRedBall.GumCommon" (Gum UI) [MIT]
- "Gum.MonoGame" (Gum UI, but Monogame) [MIT]
- "[ImGui.NET](https://github.com/ImGuiNET/ImGui.NET/)" (Additional UI Library) [MIT]
- "[Monogame](https://github.com/MonoGame/MonoGame).Framework.DesktopGL" (Monogame) [Ms-Pl / MIT]
- "Microsoft.Extensions.Logging" (Logger)
- "Microsoft.Testing.Platform" (Logger)
- "Microsoft.TestPlatform.TestHost" (Logger)
- "RandN" (Random Number Generation) [MIT]
- "Sin3D" (3D-centric Classes) [MIT]
- "[Fluent.NET](https://projectfluent.org/fluent/guide/)" (Localization) [Apache-2.0]
