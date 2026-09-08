using JetBrains.Annotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SKSSL.Tests;

[TestClass, TestSubject(typeof(Utilities.Voronoi.Voronoi))]
public class Voronoi
{
    [TestMethod]
    public void TEST_VORONOI_GEN()
    {
        using var game = new UnitGame();
        game.Run();
    }
}