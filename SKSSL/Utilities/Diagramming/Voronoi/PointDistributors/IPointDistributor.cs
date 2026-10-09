using System.Collections.Generic;

namespace SKSSL.Utilities.Voronoi.PointDistributors;

public interface IPointDistributor
{
    public void Generate(ref List<Point> points, uint amount, float maxX, float maxY, double randomness);
}