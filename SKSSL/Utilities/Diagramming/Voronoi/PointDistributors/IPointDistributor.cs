using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace SKSSL.Utilities.Voronoi.PointDistributors;

public interface IPointDistributor
{
    /// <summary>
    /// Repositions or replaces an initially seeded list of sites.
    /// Implementations must return exactly <paramref name="amount"/> points, each within
    /// [0,maxX] x [0,maxY]. Clear and repopulate <paramref name="points"/> when constructing
    /// a completely new distribution.
    /// </summary>
    void Generate(ref List<Vector2> points, uint amount, float maxX, float maxY, double randomness);
}
