# Voronoi Diagrams

SKSSL includes the code necessary to create and manipulate Voronoi diagrams; created using the Delaunay algorithm.
Below is an example of a game class that creates a voronoi diagram, alters it, and draws it.

```csharp
public class MyGameClass : SSLGame
{
    private Utilities.Voronoi.Voronoi _voronoi = null!;
    private bool generated = false;
    private VoronoiCell? _hoveredCell;

    public override void Initialize()
    {
        SpriteBatch spriteBatch = new SpriteBatch(GraphicsDevice);
        _voronoi = new Utilities.Voronoi.Voronoi(game.GraphicsDevice);
        base.Initialize();
    }

    public override void LoadContent()
    {
        
        // EU5 has around 30k~. This can generate 100k and highlight cells somewhat smoothly. The catch is that this
        //  does not guarantee to be performance when extra data like province goods is hooked-up. That might require
        //  some kind of culling.
      _voronoi.GenerateDiagram(
         // NOTE: Read the documentation for GenerateDiagram! There are a LOT of customizable 
         // parameters.
        pointCount: 4000,
        distribution: DelaunayTriangulator.PointDistribution.RandomJitter,
        boundaryMode: Utilities.Voronoi.Voronoi.VoronoiBoundaryMode.HardEdge,
        flags: Utilities.Voronoi.Voronoi.VoronoiRenderingFlags.Cells);

        for (int i = 0; i <= 2040; i++)
        {
            _voronoi.MarkCell(i);
        }
    
        _voronoi.ColorMarkedCells(Color.BlanchedAlmond, Color.DarkGreen);
        generated = true;
        base.LoadContent();
    }  
    

    private void Update(GameTime gameTime)
    {
        // This is example code for handling highlighting using the mouse.
        if (generated)
        {
            MouseState mouse = Mouse.GetState();
            Point mousePosition = new(mouse.X, mouse.Y);

            _voronoi.TryGetCellAt(mousePosition, out _hoveredCell);
            if (_hoveredCell != null)
                _voronoi.SetHighlightedCells([_hoveredCell], Color.Black);
        }
        base.Update(gameTime);
    }
}
```

Most of the parameters are optional, and will by-default instantiate a culled diagram with unique colours per-cell.
Cells are internally provided integer IDs when created.

## (Re)Coloring a Diagram

Voronoi diagrams can be recoloured by calling `.MarkCell(int)`, wherein the parameter is the integer ID of a cell.
From there, calling `ColorMarkedCells(Color, Color?)` will recolour the selected cells, plus any remaining cells with a
default "flat" colour, if desired.

## Highlighting Cells

Cells can also be highlighted with a line. First, call `TryGetCell(Point, out VoronoiCell?)` where point is
a Screen Position `System.Drawing.Point position` then call `SetHighlightedCell(VoronoiCell, Color, float)` using the
found cell. This highlight is a simple colored line around the cell.

# Incomplete

As of 20260915 there are still a lot of things to implement for the [Voronoi.cs](Voronoi.cs) class.