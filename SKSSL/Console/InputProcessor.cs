using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Input;
using TextCopy;
using Keys = Microsoft.Xna.Framework.Input.Keys;

// ReSharper disable ConvertSwitchStatementToSwitchExpression

// ReSharper disable SwitchStatementHandlesSomeKnownEnumValuesWithDefault

namespace SKSSL.Console
{
    internal class InputProcessor(CommandProcessor commandProcessor)
    {
        public event EventHandler Open = delegate { };
        public event EventHandler Close = delegate { };
        public event EventHandler PlayerCommand = delegate { };
        public event EventHandler ConsoleCommand = delegate { };

        public CommandHistory CommandHistory { get; set; } = [];

        /// <summary></summary>
        public OutputLine Buffer { get; set; } = new("", OutputLineType.Command);

        /// <summary>The resulting output lines sent to the console window.</summary>
        public List<OutputLine> Out { get; set; } = [];

        private bool isActive = false;

        #region Keypress Events

        private void Alternate_Keypress(object sender, Keys key)
        {
            if (!isActive) return; // console is opened -> accept input

            CommandHistory
                .Reset(); // WIP: May be the cause of command history's inability to recall more than one command.
            switch (key)
            {
                case Keys.Enter:
                    ExecuteBuffer();
                    ((GameConsole)sender).consoleComponent.consoleRenderer.IsJumping = true;
                    break;
                case Keys.OemMinus:
                    Buffer.Output += '-';
                    break;
                case Keys.Back:
                    if (Buffer.Output.Length > 0)
                        Buffer.Output = Buffer.Output[..^1];
                    break;
                case Keys.Tab:
                    AutoComplete();
                    break;
                case Keys.Space:
                    Buffer.Output += " ";
                    break;
                default:
                    if (IsLetterNumberKey(key) && IsPrintable(key.ToString()[0]))
                    {
                        // Convert to lowercase if it's a letter key
                        char keyChar = key >= Keys.A && key <= Keys.Z ? char.ToLower((char)key) : (char)key;
                        Buffer.Output += keyChar;
                    }

                    break;
            }
        }

        internal void EventInput_KeyDown(object sender, Keys keyPressed)
        {
            // CTRL is being held.
            if (Keyboard.GetState().IsKeyDown(Keys.LeftControl))
            {
                KeyboardState state = Keyboard.GetState();
                // CTRL + V - Paste
                if (state.IsKeyDown(Keys.V))
                {
                    var clipboard = ClipboardService.GetText();
                    AddToBuffer(clipboard);
                    return;
                }

                if (state.IsKeyDown(Keys.Back))
                {
                    // IMPL: Add CTRL-BACKSPACE to delete a word
                    return;
                }

                return;
            }

            // SHIFT is being held.
            if (Keyboard.GetState().IsKeyDown(Keys.LeftShift))
            {
                // The '_' character.
                if (Keyboard.GetState().IsKeyDown(Keys.OemMinus))
                {
                    Buffer.Output += "_";
                    return;
                }

                if (Keyboard.GetState().IsKeyDown(Keys.OemSemicolon))
                {
                    Buffer.Output += ":";
                    return;
                }

                if (Keyboard.GetState().IsKeyDown(Keys.OemQuotes))
                {
                    Buffer.Output += "\"";
                    return;
                }

                return;
            }

            // Toggle Console Key
            if (keyPressed == GameConsoleOptions.Options.ToggleKey)
            {
                ToggleConsole();
            }

            // For Alternate Keybinds
            Alternate_Keypress(sender, keyPressed);

            // Up and Down Arrows
            switch (keyPressed)
            {
                case Keys.Up: Buffer.Output = CommandHistory.Previous(); break;
                case Keys.Down: Buffer.Output = CommandHistory.Next(); break;
            }
        }

        #endregion

        /// Check if key is a letter (A-Z) or number (0-9) or minus (-)
        private static bool IsLetterNumberKey(Keys key) =>
            (key >= Keys.A && key <= Keys.Z) || (key >= Keys.D0 && key <= Keys.D9);

        private void AddToBuffer(string text)
        {
            var lines = text.Split('\n').Where(line => line != "").ToArray();
            int i;
            for (i = 0; i < lines.Length - 1; i++)
            {
                var line = lines[i];
                Buffer.Output += line;
                ExecuteBuffer();
            }

            Buffer.Output += lines[i];
        }

        public void AddToOutput(string text)
        {
            if (GameConsoleOptions.Options.OpenOnWrite)
            {
                isActive = true;
                Open(this, EventArgs.Empty);
            }

            foreach (var line in text.Split('\n'))
            {
                Out.Add(new OutputLine(line, OutputLineType.Output));
            }
        }

        private void ToggleConsole()
        {
            isActive = !isActive;
            if (isActive)
            {
                Open(this, EventArgs.Empty);
            }
            else
            {
                Close(this, EventArgs.Empty);
            }
        }

        private void ExecuteBuffer()
        {
            if (Buffer.Output.Length == 0)
            {
                return;
            }

            var output = CommandProcessor.Process(Buffer.Output).Split('\n').Where(l => l != "");
            Out.Add(new OutputLine(Buffer.Output, OutputLineType.Command));
            foreach (var line in output)
            {
                Out.Add(new OutputLine(line, OutputLineType.Output));
            }

            CommandHistory.Add(Buffer.Output);
            Buffer.Output = "";
        }

        private void AutoComplete()
        {
            var lastSpacePosition = Buffer.Output.LastIndexOf(' ');
            var textToMatch = lastSpacePosition < 0
                ? Buffer.Output
                : Buffer.Output[(lastSpacePosition + 1)..];
            IConsoleCommand? match = GetMatchingCommand(textToMatch);
            if (match == null)
            {
                return;
            }

            var restOfTheCommand = match.Handle[textToMatch.Length..];
            Buffer.Output += restOfTheCommand + " ";
        }

        private static IConsoleCommand? GetMatchingCommand(string handle)
        {
            var matchingCommands =
                GameConsoleOptions.Commands.Where(c => c.Handle.StartsWith(handle));
            return matchingCommands.FirstOrDefault();
        }

        private static bool IsPrintable(char letter)
        {
            return GameConsoleOptions.Options.Font.Characters.Contains(letter);
        }
    }
}