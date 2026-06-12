// ReSharper disable StringLiteralTypo

namespace SKSSL.Tests.TestData;

public abstract class TestPrototypes
{
  public static readonly string ExpectedOutputSingleEntry = $"""
                                                             - type: Entity
                                                               id: testa
                                                               name: test-name
                                                               description: test-desc
                                                               components:
                                                               - type: {nameof(TestBlankComponent).Replace("Component", "")}
                                                             """;

    public static readonly string ExpectedOutputYamlMultiEntry = """
                                                                 - type: Entity
                                                                   id: testb
                                                                   name: test-name
                                                                   description: test-desc

                                                                 - type: Entity
                                                                   id: testc       
                                                                   name: test-name
                                                                   description: test-desc
                                                                 """;

    public static readonly string TestYamlSingleEntry = $"""
                                                         - type: Entity
                                                           id: testa         
                                                           name: test-name
                                                           description: test-desc
                                                           components:
                                                           - type: {nameof(TestBlankComponent).Replace("Component", "")}
                                                         """;

    public const string TestYamlMultiEntry = """
                                             - type: Entity
                                               id: testb          
                                               name: test-name
                                               description: test-desc
                                             - type: Entity
                                               id: testc        
                                               name: test-name
                                               description: test-desc
                                             """;

    public const string TestYamlOverride = """
                                           # Ensure that test-a has full qualifier
                                           - type: Entity
                                             id: game:testa
                                             name: test-name-override
                                             description: test-desc-override
                                           """;
}