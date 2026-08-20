using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SKSSL.Assets;
using static Microsoft.Xna.Framework.Color;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global

namespace SKSSL.Console
{
    /// <summary>
    /// Console options to customize the look and feel of the console.
    /// </summary>
    /// <remarks>Critically depends on <see cref="SSLGame"/></remarks>
    public class GameConsoleOptions
    {
        public Keys ToggleKey { get; init; } = Keys.OemTilde;
        public Color BackgroundColor { get; init; } = new(0, 0, 0, 125);

        /// <summary>
        /// Dictates total font color over all of it.
        /// </summary>
        public Color FontColor
        {
            set => BufferColor = PastCommandColor = PastCommandOutputColor = PromptColor = CursorColor = value;
        }

        public Color BufferColor { get; set; } = White;
        public Color PastCommandColor { get; set; } = White;
        public Color PastCommandOutputColor { get; set; } = White;
        public Color PromptColor { get; set; } = White;
        public Color CursorColor { get; set; } = White;
        public float AnimationSpeed { get; set; } = 1;
        public float CursorBlinkSpeed { get; set; } = 0.5f;
        public int Height { get; set; } = 300;
        public string Prompt { get; init; } = "$";
        public char Cursor { get; set; } = '_';
        public int Padding { get; set; } = 12;
        public int Margin { get; set; } = 30;
        public bool OpenOnWrite { get; set; } = true;
        
        /// <summary>
        /// Replaceable font. In order to work, expects that the builder had indeed built a console font.
        /// </summary>
        public SpriteFont Font { get; set; }

        public Texture2D RoundedCorner { get; set; }
        internal static GameConsoleOptions Options { get; set; }
        internal static List<IConsoleCommand> Commands { get; set; } = [];

        /// <summary>
        /// The default values used from the SolKom project.
        /// </summary>
        /// <returns></returns>
        // ReSharper disable once UnusedMember.Global
        public static GameConsoleOptions SolKomDefault()
        {
            return new GameConsoleOptions
            {
                ToggleKey = Keys.OemTilde,
                Font = SSLGame.Instance.LoadFont("ConsoleFont"),
                FontColor = LawnGreen,
                Prompt = "->",
                PromptColor = Crimson,
                CursorColor = OrangeRed,
                BackgroundColor = new Color(Black, 150),
                PastCommandOutputColor = White,
                BufferColor = Gold
            };
        }
    }
}