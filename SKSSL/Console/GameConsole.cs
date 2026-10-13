using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace SKSSL.Console;

#region File Description

//-----------------------------------------------------------------------------
// MonoGame Console https://github.com/romanov/MonoGameConsole
// Forked from http://code.google.com/p/xnagameconsole/
// Edited and Refactored by LukeZurg22
// GNU GPL v3
//-----------------------------------------------------------------------------

#endregion

/// <summary>
/// A top-screen console. Only one per scene.
/// <br/><br/>
/// To Get the Console Remotely:<br/> 
/// GameConsole console = (GameConsole)this.Services.GetService(typeof(GameConsole));
/// </summary>
public class GameConsole
{
    public static GameConsoleOptions Options => GameConsoleOptions.Options;

    public static List<IConsoleCommand> Commands => GameConsoleOptions.Commands;

    /// <summary>
    /// Adds a new command to the console.
    /// </summary>
    [UsedImplicitly] // Called by Source Generator.
    public static void AddCommand(IConsoleCommand command) => Commands.Add(command);

    public bool Enabled { get; set; }

    /// <summary>
    /// Indicates whether the console is currently opened
    /// </summary>
    public bool Opened => consoleComponent.IsOpen;

    internal readonly GameConsoleComponent consoleComponent;

    # region Constructors

    public GameConsole(Game? game, SpriteBatch spriteBatch)
        : this(game, spriteBatch, Array.Empty<IConsoleCommand>(), new GameConsoleOptions())
    {
    }


    // ReSharper disable once UnusedMember.Global
    public GameConsole(Game? game, SpriteBatch spriteBatch, GameConsoleOptions options)
        : this(game, spriteBatch, Array.Empty<IConsoleCommand>(), options)
    {
    }

    public GameConsole(Game? game, SpriteBatch spriteBatch, IEnumerable<IConsoleCommand> commands,
        GameConsoleOptions options)
    {
        if (options.Font == null)
            throw new NullReferenceException("Please, provide SpriteFont for console font!");

        GameConsoleOptions.Options = options;
        GameConsoleOptions.Commands = commands.ToList();
        Enabled = true;
        consoleComponent = new GameConsoleComponent(this, game, spriteBatch);
        if (game?.Services.GetService<GameConsole>() == null)
            game?.Services.AddService(typeof(GameConsole), this);
        // ReSharper disable once SimplifyLinqExpressionUseAll
        // If theres already a game component, dispose of it so it can be re-added.
        if (game != null && game.Components.Any(p => p.GetType() == typeof(GameConsoleComponent)))
        {
            foreach (IGameComponent p in game.Components)
            {
                if (p.GetType() != typeof(GameConsoleComponent)) continue;
                game.Components.Remove(p);
                break;
            }
        }

        game?.Components.Add(consoleComponent);
    }

    #endregion

    /// <summary>
    /// Write directly to the output stream of the console
    /// </summary>
    /// <param name="text"></param>
    public void WriteLine(string text) => consoleComponent.WriteLine(text);

    private KeyboardState currentKeyboardState;
    private KeyboardState previousKeyboardState;

    private int deltaScrollWheelValue = 0;
    private int currentScrollWheelValue = 0;
    private const int scrollIntensity = 12;

    public void Update(GameTime gameTime)
    {
        #region Mouse

        // Mouse Handling
        var currentMouseState = Mouse.GetState();
        deltaScrollWheelValue = currentMouseState.ScrollWheelValue - currentScrollWheelValue;
        currentScrollWheelValue += deltaScrollWheelValue;
        switch (deltaScrollWheelValue) // Supposedly using a switch is faster. Might not matter much, but oh well!
        {
            case < 0: // scrolling down
                consoleComponent.consoleRenderer.firstCommandPositionOffset.Y -= scrollIntensity;
                break;

            case > 0: // scrolling up
                consoleComponent.consoleRenderer.firstCommandPositionOffset.Y += scrollIntensity;
                break;
        }

        #endregion

        #region Keyboard

        // Update keyboard states
        previousKeyboardState = currentKeyboardState;
        currentKeyboardState = Keyboard.GetState();

        // Check for any key press
        var pressedKey = GetPressedKey();

        // Interrupt!
        if (!pressedKey.HasValue) return;

        // Pass the pressed key as an argument to your method
        consoleComponent.inputProcesser.EventInput_KeyDown(this, pressedKey.Value);

        #endregion
    }

    private int backspacePressTicks = 0;
    private const int BackspaceDelayTicks = 5; // Adjust the number of ticks for the delay

    /// <summary>
    /// Obtains a key pressed by the user if any at all.
    /// </summary>
    /// <returns></returns>
    private Keys? GetPressedKey()
    {
        // Check if Backspace was just pressed (no need to delay)
        if (currentKeyboardState.IsKeyDown(Keys.Back) && !previousKeyboardState.IsKeyDown(Keys.Back))
        {
            return Keys.Back;
        }

        // Check if Backspace is held down and wait for a few ticks to ensure it's still pressed
        if (currentKeyboardState.IsKeyDown(Keys.Back))
        {
            if (backspacePressTicks >= BackspaceDelayTicks)
                return Keys.Back;

            backspacePressTicks++;
            return null;
        }

        // Reset the counter if Backspace is no longer pressed
        if (!currentKeyboardState.IsKeyDown(Keys.Back))
        {
            backspacePressTicks = 0;
        }

        // Check for other keys being pressed
        foreach (Keys key in Enum.GetValues<Keys>())
        {
            if (currentKeyboardState.IsKeyDown(key) && !previousKeyboardState.IsKeyDown(key))
                return key;
        }

        return null;
    }
}