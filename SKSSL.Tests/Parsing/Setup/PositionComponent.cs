using SKSSL.ECS;

namespace SKSSL.Tests.Tests.Parsing.Setup;

public record PositionIskComponent : ISKComponent;
public record OtherPositionIskComponent(int x, int y) : ISKComponent;