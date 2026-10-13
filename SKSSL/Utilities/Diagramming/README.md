# `DiagramMap`

`DiagramMap` is a dynamic, renderable cell map for MonoGame. It draws a Voronoi-style diagram using a cell palette
shader and supports a separate overlay layer, cell interaction, and per-cell color overrides.

This README describes the functionality visible in the supplied `DiagramMap` implementation and its APIs.

## Features

- Palette-based cell rendering using a dedicated shader effect.
- Separate diagram and overlay opacity settings supplied by Voronoi options.
- Cell lookup from screen coordinates through `TryGetCellAt`.
- Per-cell highlight colors rendered on an independent texture layer.
- Optional flashing highlights with a configurable interval.
- Highlight opacity control without changing stored highlight colors.
- Pixel-to-cell indexing to update the pixels belonging to a cell.

## Requirements

The implementation uses:

- C#
- MonoGame (`Microsoft.Xna.Framework` and `Microsoft.Xna.Framework.Graphics`)
- Project-specific Voronoi types, namely `VoronoiDiagramData`
- The embedded `CellPalette_OpenGL.mgfxo` shader effect from SKSSL.

The exact target framework and MonoGame version are project-specific and are not established by the code.

## Creating a map

Construct a `DiagramMap` with a graphics device and generated Voronoi data:

```csharp
DiagramMap diagramMap = new DiagramMap(graphicsDevice, voronoiDiagramData);
```

The `VoronoiDiagramMapPipeline` class has methods that streamline the process of creating diagram maps, with or without
height / displacement maps. Below is an example:

```csharp
// EU5 has around 30k~. This can generate 100k and highlight cells somewhat smoothly. The catch is that this
//  does not guarantee to be performance when extra data like province goods is hooked-up. That might require
//  some kind of culling.
const int siteCount = 1000;
DiagramMap diagram = VoronoiDiagramMapPipeline.FromHeightmap(
    _game.GraphicsDevice,
    _densityTexture,
    1920, 1080,
    new VoronoiOptions
    {
        SiteCount = siteCount,
        OverlayFlags = DiagramOverlayOptions.Sites,
    },
    new ReferenceBoundarySettings(_borderMap));
_diagramMap = diagram;
_diagramMap.SetDiagramProjection(Matrix.Identity, Matrix.Identity, _game.GraphicsDevice.Viewport.Width, _game.GraphicsDevice.Viewport.Height);

// Highlight a cell in translucent yellow.
_diagramMap.SetCellHighlight(cellId: 42, color: new Color(12, 220, 0, 160));
_diagramMap.SetHighlightOpacity(1f);

// Flash every 250 milliseconds.
_diagramMap.SetHighlightFlashing(
    enabled: true,
    interval: TimeSpan.FromMilliseconds(250)
);
```

`VoronoiDiagramData` supplies the cell-ID texture, pixel-to-cell mapping, site and cell color data, overlay colors, and
Voronoi options used to initialize the map.

## Drawing

Call `Draw` during the render phase:

```csharp
diagramMap.Draw(spriteBatch);
```

The method returns without drawing if `spriteBatch` is `null`. Otherwise, it flushes pending texture updates, configures
the palette shader, draws the diagram, and then draws the overlay if one is available.

The diagram uses the configured diagram opacity; the overlay uses the configured overlay opacity. Both are clamped to
the range `0`–`1` when drawn.

### Transform

The map's diagram transform is used for rendering and for translating screen positions back into diagram-local
coordinates. Configure it through the map's transform/projection API before drawing and querying cells.

## Selecting a cell

Use `TryGetCellAt` to retrieve the cell ID under a point, typically the mouse position:

```csharp
if (diagramMap.TryGetCellAt(mousePosition, out uint cellId))
{
    // Use cellId for selection, highlighting, or other game logic.
}
```

The method converts the supplied screen position through the inverse diagram transform. It returns `false` if the
transformed position is outside the map or if no cell owns that pixel.

## Cell highlights

Highlights are rendered through a dedicated texture, separate from the base diagram, cell color overrides, and existing
overlay. This lets you emphasize cells without replacing their normal colors.

### Highlight a cell

```csharp
diagramMap.SetCellHighlight(
    cellId: 42,
    color: new Color(255, 220, 0, 160)
);
```

- `cellId` is the cell's ID.
- `color` is the highlight color. Its alpha component controls transparency.
- An out-of-range cell ID is ignored.

Setting a highlight replaces the previously assigned highlight color for that cell.

### Clear one highlight

```csharp
diagramMap.ClearCellHighlight(42);
```

This makes the selected cell's highlight pixels transparent. An out-of-range cell ID is ignored.

### Clear all highlights

```csharp
diagramMap.ClearAllCellHighlights();
```

This clears the highlight state for all cells and makes the highlight texture transparent. It does not change base cell
colors, cell overrides, or the existing overlay.

## Flashing highlights

Flashing alternates the visibility of the highlight layer. The interval represents the duration of each visible or
hidden phase, not the duration of a complete on/off cycle.

```csharp
diagramMap.SetHighlightFlashing(
    enabled: true,
    interval: TimeSpan.FromMilliseconds(250)
);
```

With a 250 ms interval, highlight visibility toggles every quarter-second, for a complete on/off cycle of approximately
500 ms.

To stop flashing:

```csharp
diagramMap.SetHighlightFlashing(enabled: false);
```

Each call resets the flashing timer and makes the highlight layer visible immediately. If `interval` is omitted, the
previously configured interval is retained. The default interval is 500 ms per phase.

A supplied interval must be greater than `TimeSpan.Zero`; otherwise, `ArgumentOutOfRangeException` is thrown.

### Update the animation

The animation must be advanced once per game update. `UpdateHighlights(GameTime)` is private in the supplied
implementation, so call it from the appropriate update method inside `DiagramMap`, simply titled "`Update(GameTime)`.

If the class already has an update method, add the call there rather than creating a duplicate. The shown public API
does not expose `UpdateHighlights` directly.

## Highlight opacity

Set a multiplier for the entire highlight layer:

```csharp
diagramMap.SetHighlightOpacity(0.6f);
```

- `0` makes the highlight layer fully transparent.
- `1` applies the highlight colors at their stored alpha.
- Intermediate values reduce the layer's opacity.

The multiplier is applied while drawing and does not modify stored highlight colors. The current implementation does not
clamp the supplied value, so callers should pass a value in the range `0`–`1`.

## Typical workflow

```csharp
// Select a cell from the mouse position.
if (diagramMap.TryGetCellAt(mousePosition, out uint cellId))
{
    diagramMap.SetCellHighlight(
        cellId,
        new Color(255, 220, 0, 160)
    );

    diagramMap.SetHighlightFlashing(
        enabled: true,
        interval: TimeSpan.FromMilliseconds(300)
    );
}

// In DiagramMap's own update method, advance flashing:
// UpdateHighlights(gameTime);

// In the render phase:
diagramMap.Draw(spriteBatch);

// Later, remove a selection highlight:
diagramMap.ClearCellHighlight(cellId);

// Or clear all highlights:
// diagramMap.ClearAllCellHighlights();
```

## Implementation notes

- **Independent rendering layers:** The highlight texture is drawn separately, allowing highlights to coexist with the
  diagram and overlay.
- **Texture uploads:** `SetCellHighlight`, `ClearCellHighlight`, and `ClearAllCellHighlights` upload the highlight pixel
  buffer with `Texture2D.SetData`. These operations upload the full texture in the current implementation, so frequent
  highlight changes on large maps may be worth profiling.
- **Cell pixel membership:** Highlight operations use the map's per-cell pixel index. If cell membership changes
  dynamically, ensure the highlight data remains consistent with that membership.
- **Graphics resource lifetime:** `DiagramMap` creates GPU textures and an effect. Dispose of owned graphics resources
  when the map is no longer needed, following the resource ownership conventions of the surrounding project.

## API summary

| Method                                                          | Purpose                                                           |
|-----------------------------------------------------------------|-------------------------------------------------------------------|
| `Draw(SpriteBatch? spriteBatch)`                                | Draws the diagram and its overlay.                                |
| `TryGetCellAt(Point point, out uint id)`                        | Finds the cell at a screen position.                              |
| `SetCellHighlight(uint cellId, Color color)`                    | Sets a cell's highlight color.                                    |
| `ClearCellHighlight(uint cellId)`                               | Removes one cell's highlight.                                     |
| `ClearAllCellHighlights()`                                      | Removes every highlight.                                          |
| `SetHighlightFlashing(bool enabled, TimeSpan? interval = null)` | Enables/disables flashing and optionally sets the phase interval. |
| `SetHighlightOpacity(float opacity)`                            | Sets the highlight-layer opacity multiplier.                      |

`UpdateHighlights(GameTime)` and `DrawHighlights(SpriteBatch)` are private helpers in the supplied highlight
implementation.
