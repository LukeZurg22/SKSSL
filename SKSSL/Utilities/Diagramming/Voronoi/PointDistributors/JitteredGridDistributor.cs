using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace SKSSL.Utilities.Voronoi.PointDistributors;

/// <summary>
/// Example custom distributor: builds a jittered grid with exactly the requested number of sites.
/// randomness is clamped to [0,1] and controls jitter relative to each grid cell.
/// </summary>
public sealed class JitteredGridDistributor : IPointDistributor
{
    private readonly int _seed;

    public JitteredGridDistributor(int seed = 12345) => _seed = seed;

    public void Generate(ref List<Vector2> points, uint amount, float maxX, float maxY, double randomness)
    {
        if (amount == 0)
        {
            points.Clear();
            return;
        }
        if (maxX <= 0 || maxY <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxX), "Bounds must be positive.");

        int count = checked((int)amount);
        int columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(count * (double)maxX / maxY)));
        int rows = (int)Math.Ceiling(count / (double)columns);
        float cellWidth = maxX / columns;
        float cellHeight = maxY / rows;
        float jitter = (float)Math.Clamp(randomness, 0.0, 1.0);
        var random = new Random(_seed);

        points = new List<Vector2>(count);
        for (int i = 0; i < count; i++)
        {
            int column = i % columns;
            int row = i / columns;
            float x = (column + 0.5f) * cellWidth;
            float y = (row + 0.5f) * cellHeight;
            x += ((float)random.NextDouble() * 2f - 1f) * cellWidth * 0.5f * jitter;
            y += ((float)random.NextDouble() * 2f - 1f) * cellHeight * 0.5f * jitter;
            points.Add(new Vector2(
                Math.Clamp(x, 0.001f, Math.Max(0.001f, maxX - 0.001f)),
                Math.Clamp(y, 0.001f, Math.Max(0.001f, maxY - 0.001f))));
        }
    }
}
