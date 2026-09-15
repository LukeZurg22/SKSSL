# Flags

Not to be confused with the mathematical term, **Flags** as implemented here are the literal representation of
National and State flags.

The classes provided are for the abstractions and later creations of country flags. Everything is handled through the
instantiation of a [FlagMap.cs](FlagMap.cs).
To utilize the map, insert an object inheriting [ImageLayer.cs](ImageLayer.cs) or
[ShapeLayer.cs](ShapeLayer.cs) or a custom implementation that extends [IFlagLayer.cs](IFlagLayer.cs).

Further directions assume `map` is an instance of the `FlagMap` type.

## Building a Flag
Call `map.Add(IFlagLayer)` to add layers to your flag map. These layers can be Shapes or other 2D Images, which work
well for coats of arms and other iconography.

Layer transformation and translation can be adjusted at runtime. This means that before rasterization, one can `Move`,
`Resize`, and `Rotate` layers to their hearts content.

## Rasterization
To finish your map, call `map.CreateTexture(GraphicsDevice)` which will return a `Texture2D` object.
> It is _highly_ suggested that you do _not_ call this, nor `map.Rasterize()` within `Update()` or any other loops.
> This is a system intensive method that can slow your program.
> For storing and accessing a large collection of flags, you should pre-cache them as files and load them only when in
> active use.