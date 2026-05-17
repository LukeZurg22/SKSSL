namespace SKSSL.Types;

/// <summary>
/// Hash Grid set on two axes.
/// </summary>
/// <typeparam name="T">Dynamic object Type contained within internal grid cells.</typeparam>
/// <remarks>T can be a List if desired, for whatever reason.</remarks>
public class SpatialHashGrid<T> where T : class
{
    /// Maximum cell size of the hash grid. Set by the constructor.
    public readonly Vector2Int Size;

    //private readonly Dictionary<(int, int), object> _cells = new();
    private readonly T?[,] _cells;

    /// <summary>
    /// Constructor for Spacial Hash Grid.
    /// </summary>
    /// <param name="gridSizeX">Size of X-axis</param>
    /// <param name="gridSizeY">Size of Y-axis</param>
    public SpatialHashGrid(int gridSizeX = 32, int gridSizeY = 32)
    {
        _cells = new T?[gridSizeX, gridSizeY];
        Size = new Vector2Int(gridSizeX, gridSizeY);
    }

    internal (int, int) GetCell(float x, float y) => ((int)Math.Floor(x / Size.X), (int)Math.Floor(y / Size.Y));

    /// <summary>
    /// Sets cell at provided coordinates to provided value.
    /// </summary>
    /// <param name="coordinates">Coordinates of desired cell.</param>
    /// <param name="value">Value to set cell.</param>
    public object SetCell((int X, int Y) coordinates, T value)
    {
        _cells[coordinates.X, coordinates.Y] = value;
        return value;
    }

    /// <summary>
    /// Sets cell at provided coordinates to null.
    /// </summary>
    /// <param name="coordinates">Coordinates of desired cell.</param>
    public void WipeCell((int X, int Y) coordinates) => _cells[coordinates.X, coordinates.Y] = null;

    /// <summary>
    /// Safe attempt to retrieve value in cells.
    /// </summary>
    /// <param name="coordinates">Coordinates of desired cell.</param>
    /// <param name="cellObject">Object output for further use.</param>
    /// <returns>True if cell object isn't null, False if it is.</returns>
    public bool TryGetValue((int X, int Y) coordinates, out T? cellObject)
    {
        cellObject = _cells[coordinates.X, coordinates.Y];
        return cellObject != null;
    }

    /// <summary>
    /// Add provided object to cell.
    /// </summary>
    public virtual void Add(T obj, float x, float y)
    {
        (int, int) cell = GetCell(x, y);
        // When Add() is called, not possessing a cell object will force it to add.
        //  This does not consider behaviour such as item merges.
        if (!TryGetValue(cell, out T? cellObject))
        {
            cellObject = Activator.CreateInstance<T>();
            if (cellObject is not { } typeObject)
                return;
            SetCell(cell, typeObject);
            return;
        }

        if (cellObject is List<T> list)
            list.Add(obj);
    }

    /// <summary>
    /// Removes entry in a cell.
    /// If the cell contains a list of objects, it will remove the first entry of a cell's list (should it have one),
    /// or a provided object.
    /// </summary>
    public void Remove(float x, float y, T? obj = null)
    {
        (int, int) cell = GetCell(x, y);
        if (!TryGetValue(cell, out T? cellObject))
            return;
        if (cellObject is List<T> list)
        {
            if (list.Count == 0)
                WipeCell(cell);
            if (obj == null || !list.Remove(obj)) // If cant remove object, remove first entry!
                list.RemoveAt(0);
        }
        // If all else fails, delete the cell.
        else
        {
            WipeCell(cell);
        }
    }

    /// <summary>
    /// Get all objects near a point (or in a radius)
    /// For inventories, this allows one to get all items within a radius of the cursor, or a point.
    /// </summary>
    /// <returns>Enumerable list of cell entries within the provided radius.</returns>
    public IEnumerable<T> Query(float x, float y, float radius = 0)
    {
        int minX = (int)Math.Floor((x - radius) / Size.X);
        int maxX = (int)Math.Floor((x + radius) / Size.X);
        int minY = (int)Math.Floor((y - radius) / Size.Y);
        int maxY = (int)Math.Floor((y + radius) / Size.Y);

        for (int cellX = minX; cellX <= maxX; cellX++)
        for (int cellY = minY; cellY <= maxY; cellY++)
        {
            (int cx, int cy) cell = (cx: cellX, cy: cellY);
            if (TryGetValue(cell, out T? cellObject))
            {
                switch (cellObject)
                {
                    case List<T> list:
                    {
                        foreach (T obj in list)
                            yield return obj;
                        break;
                    }
                    case not null:
                        yield return cellObject;
                        break;
                }
            }
        }
    }

    /// <summary>
    /// Clear all cells.
    /// </summary>
    public void Clear()
    {
        for (var y = 0; y < _cells.GetLength(1); y++)
        for (var x = 0; x < _cells.GetLength(0); x++)
        {
            _cells[x, y] = null;
        }
    }
}