using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using XnaGame = Microsoft.Xna.Framework.Game;

namespace SKSSL.Console;

internal class ConsoleRenderer
{
    private enum State
    {
        Opened,
        Opening,
        Closed,
        Closing
    }

    public bool IsOpen => currentState == State.Opened;

    private readonly SpriteBatch? spriteBatch;
    private readonly InputProcessor inputProcessor;
    private readonly Texture2D pixel;
    private readonly int width;
    private State currentState;

    /// <summary>
    /// Position involvement of the entirety of the console window.
    /// </summary>
    private readonly Vector2 openedPosition;

    /// <summary>
    /// Position involvement of the entirety of the console window.
    /// </summary>
    private readonly Vector2 closedPosition;

    /// <summary>
    /// Position involvement of the entirety of the console window.
    /// </summary>
    private Vector2 position;

    private DateTime stateChangeTime;
    public Vector2 firstCommandPositionOffset;
    private Vector2 FirstCommandPosition => new Vector2(InnerBounds.X, InnerBounds.Y) + firstCommandPositionOffset;

    private Rectangle Bounds => new((int)position.X, (int)position.Y,
        width - GameConsoleOptions.Options.Margin * 2, GameConsoleOptions.Options.Height);

    private Rectangle InnerBounds => new(Bounds.X + GameConsoleOptions.Options.Padding,
        Bounds.Y + GameConsoleOptions.Options.Padding, Bounds.Width - GameConsoleOptions.Options.Padding,
        Bounds.Height);

    private readonly float oneCharacterWidth;
    private readonly int maxCharactersPerLine;

    public ConsoleRenderer(XnaGame? game, SpriteBatch? spriteBatch, InputProcessor inputProcessor)
    {
        currentState = State.Closed;
        if (game != null)
        {
            width = game.GraphicsDevice.Viewport.Width;
            position = closedPosition =
                new Vector2(GameConsoleOptions.Options.Margin, -GameConsoleOptions.Options.Height);
            openedPosition = new Vector2(GameConsoleOptions.Options.Margin, 0);
            this.spriteBatch = spriteBatch;
            this.inputProcessor = inputProcessor;
            pixel = new Texture2D(game.GraphicsDevice, 1, 1);
        }

        pixel?.SetData([Color.White]);
        firstCommandPositionOffset = Vector2.Zero;
        oneCharacterWidth =
            GameConsoleOptions.Options.Font.MeasureString("x").X; // IMPL: May be the source of the alignment issue.
        maxCharactersPerLine = (int)((Bounds.Width - GameConsoleOptions.Options.Padding * 2) / oneCharacterWidth);
    }

    public void Update(GameTime _)
    {
        const float TOLERANCE = 0.001f;
        if (currentState == State.Opening)
        {
            position.Y = MathHelper.SmoothStep(position.Y, openedPosition.Y,
                ((float)((DateTime.Now - stateChangeTime).TotalSeconds /
                         GameConsoleOptions.Options.AnimationSpeed)));
            if (Math.Abs(position.Y - openedPosition.Y) < TOLERANCE)
            {
                currentState = State.Opened;
            }
        }

        if (currentState != State.Closing)
            return;
        
        position.Y = MathHelper.SmoothStep(position.Y, closedPosition.Y,
            ((float)((DateTime.Now - stateChangeTime).TotalSeconds /
                     GameConsoleOptions.Options.AnimationSpeed)));
        if (Math.Abs(position.Y - closedPosition.Y) < TOLERANCE)
        {
            currentState = State.Closed;
        }
    }

    public Vector2 nextPos;

    public void Draw(GameTime gameTime)
    {
        //Do not draw if the console is closed
        if (currentState == State.Closed)
        {
            return;
        }

        spriteBatch?.Draw(pixel, Bounds, GameConsoleOptions.Options.BackgroundColor);
        // DrawRoundedEdges();

        Vector2 nextCommandPosition = DrawCommands(inputProcessor.Out, FirstCommandPosition);
        nextCommandPosition = DrawPrompt(nextCommandPosition);

        //Draw the buffer
        Vector2 bufferPosition = DrawCommand(
            inputProcessor.Buffer.ToString(),
            nextCommandPosition,
            GameConsoleOptions.Options.BufferColor);

        DrawCursor(bufferPosition, gameTime);
        IsJumping = false;
    }

    /*private void DrawRoundedEdges() // WARN: Causes a crash. Something about "texture" parameter being null.
    {
        //Bottom-left edge
        spriteBatch.Draw(GameConsoleOptions.Options.RoundedCorner,
            new Vector2(position.X, position.Y + GameConsoleOptions.Options.Height), null,
            GameConsoleOptions.Options.BackgroundColor, 0, Vector2.Zero, 1, SpriteEffects.None, 1);
        //Bottom-right edge
        spriteBatch.Draw(GameConsoleOptions.Options.RoundedCorner,
            new Vector2(position.X + Bounds.Width - GameConsoleOptions.Options.RoundedCorner.Width,
                position.Y + GameConsoleOptions.Options.Height), null, GameConsoleOptions.Options.BackgroundColor,
            0, Vector2.Zero, 1, SpriteEffects.FlipHorizontally, 1);
        //connecting bottom-rectangle
        spriteBatch.Draw(pixel,
            new Rectangle(Bounds.X + GameConsoleOptions.Options.RoundedCorner.Width,
                Bounds.Y + GameConsoleOptions.Options.Height,
                Bounds.Width - GameConsoleOptions.Options.RoundedCorner.Width * 2,
                GameConsoleOptions.Options.RoundedCorner.Height), GameConsoleOptions.Options.BackgroundColor);
    }*/

    void DrawCursor(Vector2 pos, GameTime gameTime)
    {
        if (!IsInBounds(pos.Y))
        {
            return;
        }

        var split = SplitCommand(inputProcessor.Buffer.ToString(), maxCharactersPerLine).Last();
        pos.X += GameConsoleOptions.Options.Font.MeasureString(split).X;
        pos.Y -= GameConsoleOptions.Options.Font.LineSpacing;
        spriteBatch?.DrawString(GameConsoleOptions.Options.Font,
            (int)(gameTime.TotalGameTime.TotalSeconds / GameConsoleOptions.Options.CursorBlinkSpeed) % 2 == 0
                ? GameConsoleOptions.Options.Cursor.ToString()
                : "", pos, GameConsoleOptions.Options.CursorColor);
    }

    /// <summary>
    /// Draws the specified command and returns the position of the next command to be drawn
    /// </summary>
    /// <param name="command"></param>
    /// <param name="pos"></param>
    /// <param name="color"></param>
    /// <returns></returns>
    private Vector2 DrawCommand(string command, Vector2 pos, Color color)
    {
        var splitLines = command.Length > maxCharactersPerLine
            ? SplitCommand(command, maxCharactersPerLine)
            : [command];
        foreach (var line in splitLines)
        {
            if (IsInBounds(pos.Y))
            {
                spriteBatch?.DrawString(GameConsoleOptions.Options.Font, line, pos, color);
            }

            ValidateFirstCommandPosition(pos.Y + GameConsoleOptions.Options.Font.LineSpacing);

            pos.Y += GameConsoleOptions.Options.Font.LineSpacing;
        }

        return pos;
    }

    private static IEnumerable<string> SplitCommand(string command, int max)
    {
        var lines = new List<string>();
        while (command.Length > max)
        {
            var splitCommand = command.Substring(0, max);
            lines.Add(splitCommand);
            command = command.Substring(max, command.Length - max);
        }

        lines.Add(command);
        return lines;
    }

    /// <summary>
    /// Draws the specified collection of commands and returns the position of the next command to be drawn
    /// </summary>
    /// <param name="lines"></param>
    /// <param name="pos"></param>
    /// <returns></returns>
    private Vector2 DrawCommands(IEnumerable<OutputLine> lines, Vector2 pos)
    {
        var originalX = pos.X;
        foreach (OutputLine command in lines)
        {
            if (command.Type == OutputLineType.Command)
            {
                pos = DrawPrompt(pos);
            }

            //pos.Y = DrawCommand(command.ToString(), position, GameConsoleOptions.Options.PastCommandColor).Y;
            pos.Y = DrawCommand(command.ToString(), pos,
                command.Type == OutputLineType.Command
                    ? GameConsoleOptions.Options.PastCommandColor
                    : GameConsoleOptions.Options.PastCommandOutputColor).Y;
            pos.X = originalX;
        }

        return pos;
    }

    /// <summary>
    /// Draws the prompt at the specified position and returns the position of the text that will be drawn next to it
    /// </summary>
    /// <returns></returns>
    private Vector2 DrawPrompt(Vector2 pos)
    {
        if (!IsInBounds(pos.Y))
            return pos;

        spriteBatch?.DrawString(GameConsoleOptions.Options.Font, GameConsoleOptions.Options.Prompt, pos,
            GameConsoleOptions.Options.PromptColor);
        pos.X += oneCharacterWidth * GameConsoleOptions.Options.Prompt.Length + oneCharacterWidth;
        return pos;
    }

    # region Console Open/Close

    public void Open()
    {
        if (currentState == State.Opening || currentState == State.Opened)
        {
            return;
        }

        stateChangeTime = DateTime.Now;
        currentState = State.Opening;
    }

    public void Close()
    {
        if (currentState == State.Closing || currentState == State.Closed)
        {
            return;
        }

        stateChangeTime = DateTime.Now;
        currentState = State.Closing;
    }

    #endregion

    public bool IsJumping = false;

    private void ValidateFirstCommandPosition(float nextCommandY)
    {
        // Interrupt!
        if (IsInBounds(nextCommandY) || !IsJumping) return;
        firstCommandPositionOffset.Y -= GameConsoleOptions.Options.Font.LineSpacing;
    }

    private bool IsInBounds(float yPosition)
    {
        return yPosition < openedPosition.Y + GameConsoleOptions.Options.Height - 15;
    }

    /// <summary>
    /// Clears the console and ensures the beginning cursor is within window view.
    /// </summary>
    public void Clear()
    {
        inputProcessor.Out.Clear();
        firstCommandPositionOffset = new Vector2(0, 0);
    }
}