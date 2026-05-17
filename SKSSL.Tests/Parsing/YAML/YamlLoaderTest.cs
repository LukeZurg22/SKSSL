using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SKSSL.YAML;
using VYaml.Serialization;

namespace SKSSL.Tests.Tests.Parsing.YAML;

[TestClass]
[TestSubject(typeof(YamlLoader))]
public class YamlLoaderTest
{
    public const string TestYaml = """
                                   - type: cube
                                     id: moon          
                                     name: block-name-moon
                                     description: block-description-moon
                                     textures:
                                      top: gneiss_moon

                                   - type: cube
                                     id: sand          
                                     name: block-name-sand
                                     description: block-description-sand
                                     textures:
                                       top: gneiss_moon
                                     
                                   - type: cube
                                     id: magma_warm         
                                     name: block-name-magma
                                     description: block-description-magma
                                     density: 3
                                     fragility: 3
                                     flags: Solid, Opaque
                                     textures:
                                       top: gneiss_magma_low
                                     properties:
                                       damage_on_touch: 5
                                       
                                   - type: cube
                                     id: magma_molten          
                                     name: block-name-magma
                                     description: block-description-magma
                                     density: 2
                                     fragility: 2
                                     flags: Solid, Opaque
                                     textures:
                                       top: gneiss_magma_high
                                     properties:
                                       damage_on_touch: 15

                                   - type: cube
                                     id: obsidian          
                                     name: block-name-obsidian
                                     density: 4
                                     fragility: 4
                                     textures:
                                       top: gneiss_obsidian

                                   - type: cube
                                     id: rock          
                                     name: block-name-rock
                                     density: 2
                                     fragility: 2
                                     textures:
                                       top: gneiss_rock

                                   - type: cube
                                     id: stone          
                                     name: block-name-stone
                                     density: 2
                                     fragility: 2
                                     textures:
                                       top: gneiss_stone

                                   - type: cube
                                     id: cobble          
                                     name: block-name-cobblestone
                                     density: 3
                                     fragility: 3
                                     textures:
                                       top: gneiss_cobble
                                   """;

    [TestMethod]
    [UsedImplicitly]
    public void YAML_Parser_Test()
    {
        var lines = TestYaml.Replace("\r", "").Split('\n');
        YamlLoader.ConvertLinesToYamlBlocks(lines, null);
    }

    [TestMethod]
    [UsedImplicitly]
    public void YAML_Encoding_Decoding_Test()
    {
        var lines = TestYaml.Replace("\r", "").Split('\n');
        var blocks = YamlLoader.ConvertLinesToYamlBlocks(lines, [], "");
        var combined = new Dictionary<string, byte[]>();

        foreach (IYamlBlock block in blocks)
        {
            if (!combined.ContainsKey(block.Tag))
                combined.Add(block.Tag, []);

            // Get the bytes of the block and using the type, combined the bytes with the existing ones to effectively
            //  merge the yaml entries into one.
            var bytes = block.ToBytes();
            combined[block.Tag] = combined[block.Tag].Concat(bytes).ToArray(); // using tags, not type, for testing!
        }

        // Goal is just to deserialize in bulk, rather than go for exact type.
        var random = YamlSerializer.Deserialize<object>(combined.First().Value);
    }
}