using SKSSL.Tests.TestData;

namespace SKSSL.Tests;

public abstract class TestPrototypes
{
    public static readonly string TestYamlOutputSingleEntry = $"""
                                                                   - type: Entity
                                                                     id: testa
                                                                     name: test-name
                                                                     description: test-desc
                                                                     components:
                                                                     - type: {nameof(TestBlankComponent).Replace("Component", "")}
                                                                   """;

    /// Stores two entries.
    public static readonly string TestYamlMultiEntry = """
                                                                 - type: Entity
                                                                   id: testb
                                                                   name: test-name
                                                                   description: test-desc

                                                                 - type: Entity
                                                                   id: testc       
                                                                   name: test-name
                                                                   description: test-desc
                                                                 """;

    /// Intended to override "testa"
    public const string TestYamlOverrideEntry = """
                                           # Ensure that test-a has full qualifier
                                           - type: Entity
                                             id: game:testa
                                             name: test-name-override
                                             description: test-desc-override
                                           """;
}