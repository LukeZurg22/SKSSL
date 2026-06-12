# .CSPROJ Adjustments
First, Add SKSSL As a Nuget Package Reference (Or as a submodule!)

https://www.nuget.org/packages/SKSSL/

_or add to the `.csproj` directly:_
```config
<ItemGroup>
    <PackageReference Include="SKSSL" Version="<!--Enter Version Here-->" />
</ItemGroup>
```

Then, adjust the `.csproj` to accomodate the engine.
```config

    <!-- !== ADD THE FOLLOWING TO GAME PROJECTS INHERITING / USING SKSSL AS AN ENGINE !== -->
<!--START - Enable Compiler-Generated Code-->
<PropertyGroup>
    <OutputPath>build\binaries\</OutputPath> &lt;!&ndash;Executable Is Here, SKSSL Accomodates 1 Level Above&ndash;&gt;
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <AppendRuntimeIdentifierToOutputPath>false</AppendRuntimeIdentifierToOutputPath>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    <CompilerGeneratedFilesOutputPath>Generated</CompilerGeneratedFilesOutputPath>
</PropertyGroup>
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
        <!-- Add custom pre-loaded game folders here! -->
        <!--===EXAMPLE (ROOT GAME EXAMPLE)===-->
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
        <!--END - Enable Compiler-Generated Code-->
```

This is only for a _root_ game directory as an example. A specified directory requires a folder in your project, and
**only one** of these Directory-centric content inclusions focused on that folder and all of its contents!

# SSLGame Engine Config
Make a game Class that inherits SSLGame, and call GameManager.Run<MyGameClass>() in Program.cs ; Additionally, you will
need to configure the engine's properties below.

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