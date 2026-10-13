namespace SKSSL.Tests.Classes;

public static class TestJSON
{
    public static readonly string ExpectedOutputSingleEntryJSON = $$"""
                                                                    {
                                                                      "type": "Entity",
                                                                      "id": "testa",
                                                                      "name": "test-name",
                                                                      "description": "test-desc",
                                                                      "components": [
                                                                        {
                                                                          "type": "{{nameof(TestBlankComponent).Replace("Component", "")}}"
                                                                        }
                                                                      ]
                                                                    }
                                                                    """;
}