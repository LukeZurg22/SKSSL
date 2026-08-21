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

The structure may be either a "root" layout where all assets are loaded in a single directory, which becomes
non-modular.

### Root-Centric Layout

```config
<!--
Add custom pre-loaded game folders here!
If your game has a "proper" multi-folder structure, these aren't needed.
-->
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
    <!--
        This also assumes that a load-order file is also present pre-compile.
        The inclusion of this file is NOT neccessary if your project is root-centric.
    (::see Modular Layout)
    -->
    <Content Include="load_order.yaml">
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
        <Link>..\load_order.yaml</Link>
    </Content>
</ItemGroup>
```

### Modular Layout

Meanwhile, if one alters the settings to accomodate other game folders, the _Root-Centric_ layout will be completely
ignored, and a _Modular Layout_ will be enforced.

```config

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
    <!--This load order file is crucial to have if one is using a non-root layout. (See load_order.yaml Usage)-->
    <Content Include="load_order.yaml">
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
        <Link>..\load_order.yaml</Link>
    </Content>
</ItemGroup>
```

The modular layout makes the bold assumptions that your directory structure matches the config. "game" is used as the
example, but any directory works so long as it contains the dedicated content folders.

#### Loading Multiple Directories

The `load_order.yaml` file stores the load order of the game content, and mods. The order is dictated by the `order` field. These can be
toggled using the `enabled` field by the end-user.

> Example (`load_order.yaml`)
> ```yaml
> - order: 1
>   path: game
>   enabled: true
> - order: 2
>   path: my_mod
>   enabled: false
> - order: 0
>   path: OtherMod
>   enabled: true
> ```
> Note that "root" is a reserved keyword to create a game directory based on the root of the game folder itself!
> Having the field `game_paths: []` assigned as such will automatically internally create a game directory pointed to
> the root. This is the default expected behaviour.
>
> **[WARNING!]** Once you configure your project to load a certain path that is not `root`, a base-game folder will be
> auto-created. However, dedicated sub-folders will NOT. Previous folders left behind *will be ignored!*


# Configuring SSLGame

Make a game Class that inherits the `SSLGame` class which is an extension of the MonoGame `Game` class. Call
`GameManager.Run<MyGameClassName>()` in Program.cs ; Additionally, you will
need to configure the engine's properties which are exemplified below.

## <i>[(SEE ENGINE CONFIG)](0_SETUP.ENGINE_CONFIG.md)</i>

# Settings

Configure game paths in the provided settings file, which is auto-created for you if one isn't created and specified to
be built in the inheriting project's `.csproj` file. This settings file is strictly for storing the user's settings.

# [Read Usage (Next)](1_USAGE.md)