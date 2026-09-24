using System.Collections.Generic;

namespace SKSSL.Utilities.Voronoi.PointDistributors;

public interface IPointDistributor
{
    public void Generate(ref List<Point> points, int amount, double maxX, double maxY, double randomness);
}