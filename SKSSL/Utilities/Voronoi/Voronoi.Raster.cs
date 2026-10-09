using System.Collections.Generic;
using System.Drawing;
using Microsoft.Xna.Framework.Graphics;

namespace SKSSL.Utilities.Voronoi;

public partial class Voronoi
{

    private const ushort NoRegion = ushort.MaxValue;

    private ushort[] _pixelRegions = [];
    private RegionInfo[] _regions = [];
    private Dictionary<uint, ushort> _regionSlotById = [];

    private Color[] _displayPixels = [];
    private Texture2D? _displayTexture;

    private struct RegionInfo
    {
        public uint Id;
        public Color Color;
        public bool IsActive;
    }
}