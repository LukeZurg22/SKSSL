using System.Collections.Generic;

namespace SKSSL.Utilities;

public partial class DiagramMap
{
    private readonly int[] _renderCellIdByPixel;
    private Dictionary<ulong, int> _renderIdByCellRegion;
    private int[] _renderParentCellId;

    private static ulong MakeCellRegionKey(int cellId, int regionId) => ((ulong)(uint)regionId << 32) | (uint)cellId;

    private void UpdateDisplayPixelsFromRenderIds()
    {
        for (int i = 0; i < _renderCellIdByPixel.Length; i++)
        {
            int renderId = _renderCellIdByPixel[i];
            _displayPixels[i] = renderId >= 0 ? ((uint)renderId).EncodeAsColor() : EmptyCellIdColor;
        }
    }
}