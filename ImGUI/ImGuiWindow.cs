using ImGuiNET;
using Microsoft.Xna.Framework;

// ReSharper disable UnusedParameter.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable VirtualMemberNeverOverridden.Global
// ReSharper disable FieldCanBeMadeReadOnly.Local
// ReSharper disable PublicConstructorInAbstractClass
// ReSharper disable MemberCanBeProtected.Global
// ReSharper disable UnusedType.Global

#pragma warning disable CS0414 // Field is assigned but its value is never used
namespace SKSSL.ImGUI;

/// <summary>
/// Window template with overridable flags.
/// </summary>
public abstract class ImGuiWindow
{
    //@formatter:off
    public virtual bool show_app_console        => false;
    public virtual bool no_titlebar             => false;
    public virtual bool no_scrollbar            => false;
    public virtual bool no_menu                 => false;
    public virtual bool no_move                 => false;
    public virtual bool no_resize               => false;
    public virtual bool no_collapse             => false;
    public virtual bool no_nav                  => false;
    public virtual bool no_background           => false;
    public virtual bool no_bring_to_front       => false;
    public virtual bool show_app_main_menu_bar  => false;
    //@formatter:on

    public string Title;
    public bool IsVisible = true;

    private System.Numerics.Vector2 _initialPosition;
    private System.Numerics.Vector2 _initialSize;

    public ImGuiWindow(string title, Vector2? position = null, Vector2? size = null)
    {
        Title = title;

        _initialPosition = position?.ToNumerics() ?? new Vector2(450, 20).ToNumerics();
        _initialSize = size?.ToNumerics() ?? new Vector2(550, 680).ToNumerics();
    }

    public ImGuiWindowFlags GetWindowFlags()
    {
        //@formatter:off
        ImGuiWindowFlags windowFlags = 0;
        if (no_titlebar)        windowFlags |= ImGuiWindowFlags.NoTitleBar;
        if (no_scrollbar)       windowFlags |= ImGuiWindowFlags.NoScrollbar;
        if (!no_menu)           windowFlags |= ImGuiWindowFlags.MenuBar;
        if (no_move)            windowFlags |= ImGuiWindowFlags.NoMove;
        if (no_resize)          windowFlags |= ImGuiWindowFlags.NoResize;
        if (no_collapse)        windowFlags |= ImGuiWindowFlags.NoCollapse;
        if (no_nav)             windowFlags |= ImGuiWindowFlags.NoNav;
        if (no_background)      windowFlags |= ImGuiWindowFlags.NoBackground;
        if (no_bring_to_front)  windowFlags |= ImGuiWindowFlags.NoBringToFrontOnFocus;
        //if (no_close) { } // p_open = null;
        //bool p_open = true;
        //@formatter:on
        return windowFlags;
    }
    
    /// <summary>
    /// Overridable draw call if the existing abstraction of .Render() isn't thorough enough.
    /// </summary>
    /// <param name="gameTime"></param>
    public virtual void Draw(GameTime gameTime)
    {
        // Simply cut drawing short w. visibility toggle.
        if (!IsVisible)
            return;

        ImGui.SetNextWindowPos(_initialPosition, ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(_initialSize, ImGuiCond.FirstUseEver);

        //Main body
        if (!ImGui.Begin(Title, GetWindowFlags()))
        {
            ImGui.End();
            return;
        }

        Render(gameTime); // Render implemented game window content code.

        ImGui.End();
    }

    /// <summary>
    /// Game-implementation for how this UI should render. Call <see cref="ImGui.Begin(string)"/> is already done
    /// earlier, and .End() is called automatically. This should only be written to implement window content.
    /// </summary>
    /// <remarks>
    /// DO NOT CALL THIS!<br/>
    /// Call .Draw(GameTime) instead!
    /// </remarks>
    public abstract void Render(GameTime gameTime);
}