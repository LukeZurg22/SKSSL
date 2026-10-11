using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace SKSSL.Utilities.Voronoi.PointDistributors;

// ReSharper disable once UnusedType.Global
public class DistributorRandomSystem : IPointDistributor
{
    public void Generate(ref List<Vector2> points, uint amount, float maxX, float maxY, double randomness)
    {
        Random random = Random.Shared;
        for (int i = points.Count; i < amount; i++)
            points.Add(new Vector2((float)(random.NextDouble() * maxX), (float)(random.NextDouble() * maxY)));
    }
}