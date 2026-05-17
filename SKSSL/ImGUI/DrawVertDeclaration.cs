using ImGuiNET;
using Microsoft.Xna.Framework.Graphics;

namespace SKSSL.ImGUI;

public static unsafe class DrawVertDeclaration
{
    public static readonly VertexDeclaration Declaration;

    public static readonly int Size;

    static DrawVertDeclaration()
    {
        Size = sizeof(ImDrawVert);

        Declaration = new VertexDeclaration(Size,
            // Position
            new VertexElement(0, VertexElementFormat.Vector2, VertexElementUsage.Position, 0),
            // UV
            new VertexElement(8, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
            // Color
            new VertexElement(16, VertexElementFormat.Color, VertexElementUsage.Color, 0)
        );
    }
}