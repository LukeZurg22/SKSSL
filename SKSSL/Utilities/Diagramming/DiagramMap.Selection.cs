using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

// ReSharper disable ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedMember.Global

namespace SKSSL.Utilities;

public partial class DiagramMap
{
    private int CellCount => _sites.Length;
    private readonly Vector2[] _sites;
    private readonly int[][] _cellNeighbors; // Each cell's edge-adjacent neighboring cell IDs.
    private const string error_cannotSelectCellsFromEmptyDiagram = "Cannot select cells from an empty diagram.";

    /// <param name="cellId">Zero-based Id (and index) of a cell for site[] navigation.</param>
    /// <returns><see cref="Vector2"/> position of a provided Cell ID.</returns>
    public Vector2 GetCellPosition(int cellId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(CellCount);
        ArgumentOutOfRangeException.ThrowIfNegative(cellId);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(cellId, CellCount);
        return _sites[cellId];
    }

    #region "Raw" Selectors

    /// <summary>
    /// Get all cell ids.
    /// </summary>
    /// <returns></returns>
    /// <remarks>It's best not to use this, honestly. It's here for posterity and weird edge cases.</remarks>
    public IEnumerable<int> SelectAllCells()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(CellCount);
        for (int i = 0; i < CellCount; i++) yield return i;
    }

    /// <summary>
    /// Select a random cell from the graph at random.
    /// </summary>
    /// <returns></returns>
    public int SelectRandomCell()
    {
        if (CellCount <= 0) throw new InvalidOperationException(error_cannotSelectCellsFromEmptyDiagram);
        return RNG.RN(0, CellCount);
    }

    /// <summary>
    /// Select a set of random cells with a number of attempts. Varies in size.
    /// </summary>
    /// <param name="attempts"></param>
    /// <returns></returns>
    public IEnumerable<int> SelectRandomCells(int attempts)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(attempts);
        if (CellCount <= 0) throw new InvalidOperationException(error_cannotSelectCellsFromEmptyDiagram);
        HashSet<int> cells = [];
        Parallel.For(0, attempts, _ => { cells.Add(SelectRandomCell()); });
        return cells;
    }

    /// <summary>
    /// Select a set of cells. Minimum and Maximum are inclusive.
    /// </summary>
    /// <returns></returns>
    public IEnumerable<int> SelectCellRange(int min, int max)
    {
        //@formatter:off
        if (min >= 0 && max <= CellCount) for (int i = min; i <= max; i++) yield return i;
        else throw new IndexOutOfRangeException($"Min ({min})/Max ({max}) out of acceptable cell range.");
        //@formatter:on
    }

    #endregion

    #region Selectors by Distributions

    /// <summary>
    /// Selects up to count distinct cells according to their relative weights.
    ///
    /// A weight must be finite and positive for its cell to be eligible.
    /// Higher weights make cells more likely to be selected.
    /// </summary>
    /// <code>
    /// var cells = SelectCellsByDistribution(100, cellId => cellWeights[cellId]);
    /// </code>
    public IEnumerable<int> SelectCellsByDistribution(int count, Func<int, double> weightSelector)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentNullException.ThrowIfNull(weightSelector);

        if (count == 0 || CellCount == 0) return Array.Empty<int>();
        double[] weights = CreateCellWeights(weightSelector);
        var order = BuildWeightedRandomOrder(weights);
        if (order.Count > count) order.RemoveRange(count, order.Count - count);
        return order;
    }

    /// <summary>
    /// Selects up to groupCount disjoint, connected cell clumps.
    ///
    /// Each clump receives a random target size between the inclusive
    /// minimum and maximum. A clump can be smaller if it runs out of
    /// eligible, connected neighbors.
    ///
    /// When supplied, weightSelector controls the likelihood of selecting
    /// each cell both as a seed and during clump growth.
    /// </summary>
    public IEnumerable<List<int>> SelectCellClumps(
        int groupCount,
        int minCellsPerClump,
        int maxCellsPerClump,
        Func<int, double>? weightSelector = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(groupCount);
        ArgumentOutOfRangeException.ThrowIfLessThan(minCellsPerClump, 1);

        if (maxCellsPerClump < minCellsPerClump)
        {
            const string m_1 = "Maximum clump size must be at least the minimum size.";
            throw new ArgumentOutOfRangeException(nameof(maxCellsPerClump), m_1);
        }

        if (groupCount == 0 || CellCount == 0)
            return Array.Empty<List<int>>();

        int maxTargetSize = Math.Min(maxCellsPerClump, CellCount);

        if (minCellsPerClump > maxTargetSize)
        {
            const string m_2 = "Minimum clump size cannot exceed the cell count.";
            throw new ArgumentOutOfRangeException(nameof(minCellsPerClump), m_2);
        }

        double[] weights = CreateCellWeights(weightSelector);
        var seedOrder = BuildWeightedRandomOrder(weights);

        var claimed = new bool[CellCount];
        var groups = new List<List<int>>(Math.Min(groupCount, CellCount));

        int seedCursor = 0;

        for (int groupIndex = 0; groupIndex < groupCount; groupIndex++)
        {
            // Find the next eligible, unclaimed seed.
            while (seedCursor < seedOrder.Count && claimed[seedOrder[seedCursor]]) seedCursor++;
            if (seedCursor >= seedOrder.Count)
                break;

            int seed = seedOrder[seedCursor++];
            int targetSize = Random.Shared.Next(minCellsPerClump, maxTargetSize + 1);
            var group = new List<int>(targetSize) { seed };
            claimed[seed] = true;

            // The queue prioritizes cells using weighted exponential
            // arrival times, allowing weighted frontier growth.
            var frontier = new PriorityQueue<int, double>();
            var inFrontier = new HashSet<int>();
            double currentTime = 0d;

            AddClumpFrontierNeighbors(seed, claimed, weights, inFrontier, frontier, currentTime);
            while (group.Count < targetSize && frontier.TryDequeue(out int nextCell, out double eventTime))
            {
                currentTime = eventTime;
                inFrontier.Remove(nextCell);
                if (claimed[nextCell])
                    continue;

                claimed[nextCell] = true;
                group.Add(nextCell);
                AddClumpFrontierNeighbors(nextCell, claimed, weights, inFrontier, frontier, currentTime);
            }

            groups.Add(group);
        }

        return groups;
    }

    #endregion

    #region Selector Factories

    /// <summary>
    /// Creates a selector that gives every cell equal weight.
    /// </summary>
    public static Func<int, double> CreateUniformWeightSelector() => static _ => 1d;

    /// <summary>
    /// Creates a Gaussian distribution centered on a specified point.
    /// Cells near the center have higher weights; distant cells have lower weights.
    ///
    /// The standard deviation controls how quickly the weights decay.
    /// A minimum weight of zero allows extremely distant cells to become ineligible.
    /// </summary>
    public static Func<int, double> CreateGaussianWeightSelector(
        Func<int, Vector2> positionSelector,
        Vector2 center,
        double standardDeviation,
        double minimumWeight = 0d)
    {
        ArgumentNullException.ThrowIfNull(positionSelector);
        if (!double.IsFinite(standardDeviation) || standardDeviation <= 0d)
            throw new ArgumentOutOfRangeException(nameof(standardDeviation));

        ValidateMinimumWeight(minimumWeight);
        if (!float.IsFinite(center.X) || !float.IsFinite(center.Y))
            throw new ArgumentOutOfRangeException(nameof(center));

        double variance = standardDeviation * standardDeviation;

        return cellId =>
        {
            Vector2 position = positionSelector(cellId);
            if (!float.IsFinite(position.X) || !float.IsFinite(position.Y)) return 0d;
            double dx = (double)position.X - center.X;
            double dy = (double)position.Y - center.Y;
            double distanceSquared = dx * dx + dy * dy;
            double gaussian = Math.Exp(-0.5d * distanceSquared / variance);
            return minimumWeight + (1d - minimumWeight) * gaussian;
        };
    }

    /// <summary>
    /// Creates a radial distribution with maximum weight at the center,
    /// decreasing linearly with distance raised to falloffPower.
    ///
    /// Cells outside the radius receive minimumWeight. When minimumWeight
    /// is zero, cells outside the radius are excluded from selection.
    /// </summary>
    public static Func<int, double> CreateRadialWeightSelector(
        Func<int, Vector2> positionSelector,
        Vector2 center,
        double radius,
        double minimumWeight = 0d,
        double falloffPower = 1d)
    {
        ArgumentNullException.ThrowIfNull(positionSelector);

        if (!double.IsFinite(radius) || radius <= 0d)
            throw new ArgumentOutOfRangeException(nameof(radius));

        if (!double.IsFinite(falloffPower) || falloffPower <= 0d)
            throw new ArgumentOutOfRangeException(nameof(falloffPower));

        ValidateMinimumWeight(minimumWeight);
        if (!float.IsFinite(center.X) || !float.IsFinite(center.Y))
            throw new ArgumentOutOfRangeException(nameof(center));

        double inverseRadius = 1d / radius;

        return cellId =>
        {
            Vector2 position = positionSelector(cellId);
            if (!float.IsFinite(position.X) || !float.IsFinite(position.Y)) return 0d;
            double dx = (double)position.X - center.X;
            double dy = (double)position.Y - center.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            double falloff = Math.Clamp(1d - distance * inverseRadius, 0d, 1d);
            return minimumWeight + (1d - minimumWeight) * Math.Pow(falloff, falloffPower);
        };
    }

    #endregion

    #region Helpers

    private void AddClumpFrontierNeighbors(
        int cellId,
        bool[] claimed,
        double[] weights,
        HashSet<int> inFrontier,
        PriorityQueue<int, double> frontier,
        double currentTime)
    {
        foreach (int neighborId in _cellNeighbors[cellId])
        {
            if (claimed[neighborId] || weights[neighborId] <= 0d || !inFrontier.Add(neighborId))
                continue;

            double arrivalTime = currentTime + NextWeightedDelay(weights[neighborId]);
            frontier.Enqueue(neighborId, arrivalTime);
        }
    }

    private static void ValidateMinimumWeight(double minimumWeight)
    {
        if (!double.IsFinite(minimumWeight) || minimumWeight < 0d || minimumWeight > 1d)
            throw new ArgumentOutOfRangeException(nameof(minimumWeight), "Minimum weight must be between 0 and 1.");
    }

    private double[] CreateCellWeights(Func<int, double>? weightSelector)
    {
        var weights = new double[CellCount];
        double maxWeight = 0d;
        for (int id = 0; id < CellCount; id++)
        {
            double weight = weightSelector?.Invoke(id) ?? 1d;
            if (!double.IsFinite(weight) || weight <= 0d) continue;
            weights[id] = weight;
            maxWeight = Math.Max(maxWeight, weight);
        }

        // Normalize to avoid unnecessary numerical overflow.
        if (!(maxWeight > 0d)) return weights;
        for (int id = 0; id < weights.Length; id++) weights[id] /= maxWeight;
        return weights;
    }

    private static List<int> BuildWeightedRandomOrder(double[] weights)
    {
        // Exponential-race sampling produces a weighted random ordering.
        // Sorting the keys gives a weighted sample without replacement.
        var ranked = new List<(int CellId, double Key)>(weights.Length);
        for (int id = 0; id < weights.Length; id++)
        {
            double weight = weights[id];
            if (weight <= 0d) continue;

            ranked.Add((id, NextWeightedDelay(weight)));
        }

        ranked.Sort(static (a, b) => a.Key.CompareTo(b.Key));
        var result = new List<int>(ranked.Count);
        foreach ((int CellId, double Key) entry in ranked)
            result.Add(entry.CellId);

        return result;
    }

    private static double NextWeightedDelay(double weight)
    {
        double u;
        do u = Random.Shared.NextDouble();
        while (u <= 0d);
        return -Math.Log(u) / weight;
    }

    private static int[][] BuildCellAdjacency(int[] pixelToCellId, int width, int height, int cellCount)
    {
        var adjacency = new HashSet<int>?[cellCount];

        for (int y = 0; y < height; y++)
        {
            int rowStart = y * width;
            for (int x = 0; x < width; x++)
            {
                int index = rowStart + x;
                int cellId = pixelToCellId[index];
                if (x + 1 < width) Connect(cellId, pixelToCellId[index + 1]); // Right neighbor.
                if (y + 1 < height) Connect(cellId, pixelToCellId[index + width]); // Bottom neighbor.
            }
        }

        var result = new int[cellCount][];
        for (int id = 0; id < cellCount; id++)
        {
            if (adjacency[id] is { } neighbors)
            {
                result[id] = new int[neighbors.Count];
                neighbors.CopyTo(result[id]);
            }
            else result[id] = [];
        }

        return result;

        void Connect(int a, int b)
        {
            // Ignore unowned pixels, invalid IDs, and the same cell.
            if ((uint)a >= (uint)cellCount || (uint)b >= (uint)cellCount || a == b) return;
            (adjacency[a] ??= []).Add(b);
            (adjacency[b] ??= []).Add(a);
        }
    }

    #endregion
}