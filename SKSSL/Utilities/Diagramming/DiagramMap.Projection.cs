using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SKSSL.Utilities;

public partial class DiagramMap
{
    private Matrix _diagramTransform = Matrix.Identity;

    // ReSharper disable once MemberCanBePrivate.Global
    public void SetDiagramProjection(Matrix world, Matrix view, int? width = null, int? height = null)
    {
        width ??= _displayMap.Width;
        height ??= _displayMap.Height;

        _diagramTransform = world * view;
    }

    // ReSharper disable once MemberCanBePrivate.Global
    // ReSharper disable once UnusedMember.Global
    public void SetScreenProjection(Matrix world, Matrix view)
    {
        Viewport viewport = _graphics.Viewport;
        _diagramTransform = world * view;
    }
}