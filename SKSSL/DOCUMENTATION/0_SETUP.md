# .CSPROJ Adjustments

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

# SSLGame

Make game Class inherit SSLGame, and call GameManager.Run<MyGameClass>() in Program.cs<br/>

## GUM

A Gum project file name can be overridden in the Static variable "GumFile", which assumes the file is located in
Content/Gum/\<file name + extension\>. Change this in your special `Game.cs` class' static constructor, much like
`UsesECS`.
> The way a GUM file is loaded may change!

## Game Content Loader
By default this requires the ECS to be enabled! The GameContentLoader class is an abstract inheritable class that allows a developer to create their own loader that
couples into the system. They are provided abstract [De]Serialize methods to override to turn text into Prototype lists.

The Content Loaders exclusively handle prototyping, nothing more. Supply your own extensions, and your own logic!

    protected override string[] Extensions => [...];
The Load method may also be overwritten, which will cease any Deserialization calls it would normally make. The sky
is the limit! Overriding the Load call will allow one to circumvent the UsesECS check. Interacting with the prototype
registries whilst UsesECS is disabled may cause unexpected behaviour, or **crashes**.

## Example Class

```csharp
public class MyGameClass : SSLGame
{
    static MyGameClass()
    {
        // Each of these are optional!
        UseECS = true;
        // Neglecting GUM has no consequences.
        //  It used to, though...
        GumFile = "MyGumFile.gumx";
        // The YAMLLoader from SolKom is default
        //  if this is not assigned.
        GameContentLoader = new MyContentLoader();
    }
    
    ... /*remaining game / class logic*/
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