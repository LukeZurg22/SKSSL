# .CSPROJ Adjustments

First, Add SKSSL As a Nuget Package Reference (Or as a submodule!)

https://www.nuget.org/packages/SKSSL/

_or add to the `.csproj` directly:_

```config

<ItemGroup>
    <PackageReference Include="SKSSL" Version="<!--Enter Version Here-->" />

</ItemGroup>
```

Then, adjust the `.csproj` to accomodate the engine. The following must be added to the `.csproj` file of the project
using SKSSL. It relies on source-generated code (compile-time reflection for registries, components, etc.)
whose generator's config must not be altered.

```config
<put_the_between_stuff_in_.csproj_but_not_this_tag_please>
    
<!--START - Enable Compiler-Generated Code-->
<PropertyGroup>
    &lt;!&ndash;Override the compilation output directory. SKSSL Accomodates 1 Level Above&ndash;&gt;
    <OutputPath>build\binaries\</OutputPath>
    &lt;!&ndash;Target framework (i.e. linux, win-x64, etc.) isn't needed.&ndash;&gt;
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <AppendRuntimeIdentifierToOutputPath>false</AppendRuntimeIdentifierToOutputPath>
    &lt;!&ndash;Crucial for Source Generator to Work&ndash;&gt;
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    &lt;!&ndash;Do NOT change this! This is for the Source Gen.&ndash;&gt;
    <CompilerGeneratedFilesOutputPath>Generated</CompilerGeneratedFilesOutputPath>
</PropertyGroup>

<!--Removes outdated Source-Gen code as a cleanup before-hand.-->
<Target Name="CleanBuildFolder" BeforeTargets="BeforeBuild">
<RemoveDir Directories="build" Condition="Exists(build)"/>
</Target>
<ItemGroup>
<ProjectReference Include="..\SKSSL.Generator\SKSSL.Generator.csproj"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false"/>
<ProjectReference Include="..\SKSSL\SKSSL.Engine.csproj"/>
<Compile Remove="$(CompilerGeneratedFilesOutputPath)/**/*.cs"/>
<None Include="$(CompilerGeneratedFilesOutputPath)/**/*.cs"/>
</ItemGroup>
<!--END - Enable Compiler-Generated Code-->
    
</put_the_between_stuff_in_.csproj_but_not_this_tag_please>
```

## Forcing Default Structure
Some projects may choose to begin with a dedicated "game" folder to store the game data, and with a matching
directory layout for all development assets built directly into the game's— ignorant of the MonoGame Content 
Builder —output content.

The structure may be either a "root" layout where all assets are loaded in a single directory, which becomes non-modular.
### Root-Centric Layout
```config

<copy_only_config_between_this_tag_and_not_this_tag>
    
        <!-- Add custom pre-loaded game folders here! -->
<ItemGroup>
<Content Include="prototypes\**\*.*">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <Link>..\prototypes\%(RecursiveDir)%(Filename)%(Extension)</Link>
</Content>
<Content Include="textures\**\*.*">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <Link>..\textures\%(RecursiveDir)%(Filename)%(Extension)</Link>
</Content>
<Content Include="localization\**\*.*">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <Link>..\localization\%(RecursiveDir)%(Filename)%(Extension)</Link>
</Content>
<!--This assumes that a settings file is present in your game project pre-compile.-->
<Content Include="settings.yaml">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <Link>..\settings.yaml</Link>
</Content>
</ItemGroup>
    
</copy_only_config_between_this_tag_and_not_this_tag>
```

### Modular Layout
Meanwhile, if one alters the settings to accomodate other game folders, the _Root-Centric_ layout will be completely
ignored, and a _Modular Layout_ will be enforced.
```config

<copy_only_config_between_this_tag_and_not_this_tag>
    
    <ItemGroup>
        <Content Include="game\prototypes\**\*.*">
            <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
            <Link>..game\prototypes\%(RecursiveDir)%(Filename)%(Extension)</Link>
        </Content>
        <Content Include="game\textures\**\*.*">
            <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
            <Link>..game\textures\%(RecursiveDir)%(Filename)%(Extension)</Link>
        </Content>
        <Content Include="game\localization\**\*.*">
            <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
            <Link>..game\localization\%(RecursiveDir)%(Filename)%(Extension)</Link>
        </Content>
    
        <!--This assumes that a settings file is present in your game project pre-compile.-->
        <Content Include="settings.yaml">
            <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
            <Link>..\settings.yaml</Link>
        </Content>
    </ItemGroup>
    
</copy_only_config_between_this_tag_and_not_this_tag>
```
The modular layout makes the bold assumptions that your directory structure matches the config. "game" is used as the
example, but any directory works so long as it contains the dedicated content folders.

# Configuring SSLGame

Make a game Class that inherits the `SSLGame` class which is an extension of the MonoGame `Game` class. Call 
`GameManager.Run<MyGameClassName>()` in Program.cs ; Additionally, you will
need to configure the engine's properties which are exemplified below.

## <i>[(SEE ENGINE CONFIG)](0_SETUP.ENGINE_CONFIG.md)</i>

```csharp
public class MyGame : SSLGame
{
    static MyGame()
    {
        Config = new EngineConfig()
        {
            UseECS = <bool>,    // Toggles ECS. Defaults to "true"
            GumFile = <string>, // "<name>.gumx" Defaults to "" (ignored)
            ContentLoader = new <Type of PrototypeLoader>(), // Content loader. Defaults to YamlLoader.
            //...
            //etc.
        };
    }
}
```

# Settings

Configure game paths in the provided settings file, which is auto-created for you if one isn't created and specified to
be built in the inheriting project's `.csproj` file.
> # Configuring Game Paths / Directories
> Note that "root" is a reserved keyword to create a game directory based on the root of the game folder itself!
> Having the field `game_paths: []` assigned as such will automatically internally create a game directory pointed to
> the root. This is the default expected behaviour.
>
> [WARNING!] Once you configure your project to load a certain path, the base folder (assume it is not a root) will be
> created, but dedicated sub-folders will NOT. Previous folders left behind *will be ignored!*

# [Read Usage (Next)](1_USAGE.md)