using System;
using System.Collections.Generic;
using static System.Math;

namespace SKSSL.Utilities.Voronoi.PointDistributors;


public class DistributorRandomJitter : IPointDistributor
{
    public void Generate(ref List<Point> points, int amount, double maxX, double maxY, double randomness)
    {
        Random random = Random.Shared;
        
        double aspect = maxX / maxY;
        int columns = Max(1, (int)Sqrt(amount * aspect));
        int rows = Max(1, (int)Ceiling((double)amount / columns));
        double cellWidth = maxX / columns;
        double cellHeight = maxY / rows;

        for (int y = 0; y < rows && points.Count < amount; y++)
        for (int x = 0; x < columns && points.Count < amount; x++)
        {
            double pX = (x + 0.5 + (random.NextDouble() - 0.5) * randomness) * cellWidth;
            double pY =
                (y + 0.5 + (random.NextDouble() - 0.5) * randomness)
                * cellHeight;

            pX = Clamp(pX, 0, maxX);
            pY = Clamp(pY, 0, maxY);

            points.Add(new Point(pX, pY));
        }
    }
}