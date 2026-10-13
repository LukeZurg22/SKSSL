using JetBrains.Annotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SKSSL.Tests.Classes;

namespace SKSSL.Tests;

[TestClass, TestSubject(typeof(ShaderLoaderTest))]
public class ShaderLoaderTest
{
    [TestMethod, UsedImplicitly]
    public void TEST_SHADER_EFFECT_LOAD()
    {
        using var game = new UnitGame(() => { }, () => { }, _ => { }, _ => { });
        Shaders.EffectLoader.LoadEmbedded(game.GraphicsDevice, "CellPalette_OpenGL.mgfxo");
    }
}