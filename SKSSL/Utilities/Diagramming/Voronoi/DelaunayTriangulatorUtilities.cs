using System;
using System.Runtime.CompilerServices;

namespace SKSSL.Utilities.Voronoi;

public partial class DelaunayTriangulator
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void RemoveNeighbor(Triangle triangle, Triangle neighbor)
    {
        if (triangle.Neighbor0 == neighbor)
        {
            triangle.Neighbor0 = null;
            return;
        }

        if (triangle.Neighbor1 == neighbor)
        {
            triangle.Neighbor1 = null;
            return;
        }

        if (triangle.Neighbor2 == neighbor)
        {
            triangle.Neighbor2 = null;
        }
    }

    // ReSharper disable once UnusedMember.Local
    private static void ResetTriangle(Triangle triangle)
    {
        triangle.Alive = true;

        triangle.VisitStamp = 0;
        triangle.BadStamp = 0;

        triangle.Neighbor0 = null;
        triangle.Neighbor1 = null;
        triangle.Neighbor2 = null;
    }

    private static void ConnectTriangles(Triangle a, Triangle b, Point p1, Point p2)
    {
        int edgeA = a.IndexOfEdge(p1, p2);
        int edgeB = b.IndexOfEdge(p1, p2);

#if DEBUG
        if (edgeA < 0 || edgeB < 0)
            throw new InvalidOperationException("Triangles do not share the specified edge.");
#endif

        a.SetNeighbor(edgeA, b);
        b.SetNeighbor(edgeB, a);
    }
    
    private static void RemoveTriangleTopology(Triangle triangle)
    {
        RemoveNeighborIfAlive(triangle.Neighbor0, triangle);
        RemoveNeighborIfAlive(triangle.Neighbor1, triangle);
        RemoveNeighborIfAlive(triangle.Neighbor2, triangle);

        triangle.Neighbor0 = null;
        triangle.Neighbor1 = null;
        triangle.Neighbor2 = null;

        triangle.Vertices[0].AdjacentTriangles.Remove(triangle);
        triangle.Vertices[1].AdjacentTriangles.Remove(triangle);
        triangle.Vertices[2].AdjacentTriangles.Remove(triangle);
    }

    private static void RemoveNeighborIfAlive(Triangle? neighbor, Triangle removed)
    {
        if (neighbor is { Alive: true })
        {
            RemoveNeighbor(neighbor, removed);
        }
    }
}