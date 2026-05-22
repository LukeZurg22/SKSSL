# .CSPROJ Properties
- TargetFramework = net9.0
- ImplicitUsings = enable
> Vital. SKSSL uses implicit usings everywhere.
- Nullable = enable
- MonoGamePlatform = DesktopGL
> For OpenGL/cross-platform

> Property Group (For Source Generator to work)
> - EmitCompilerGeneratedFiles = true
> - CompilerGeneratedFilesOutputPath = Generated</CompilerGeneratedFilesOutputPath>

# Item Group (References)
      "FlatRedBall.GumCommon"
      "Fluent.Net"
      "Gum.MonoGame"
      "ImGui.NET"
      "ImGUI.NET.ToolBox"
      "MemoryPack"
      "Microsoft.CodeAnalysis.Common"
      "Microsoft.CodeAnalysis.CSharp"
      "Microsoft.Extensions.Logging"
      "Microsoft.Extensions.Logging.Abstractions
      "Microsoft.Extensions.Logging.Console"
      "Microsoft.Testing.Platform"
      "Microsoft.TestPlatform.TestHost"
      "MonoGame.Framework.DesktopGL"
      "RandN"
      "VYaml"

# SSLGame
Make game Class inherit SSLGame, and call GameManager.Run<MyGameClass>() in Program.cs

# ECS
- All Components are records inheriting the base "Component" record.
- All system classes must implement the [RegisterSystem] attribute.
- All custom prototype- types are records inheriting the base "Prototype" record.
- Toggle SSLGame.UsesECS to true in a static constructor to use ECS>