using System;
using Microsoft.Xna.Framework.Graphics;

namespace SKSSL;

public partial class TextureManager
{
    public sealed class TextureLease : IDisposable
    {
        private readonly Action _release;

        private bool _disposed;

        internal TextureLease(Texture2D texture, Action release)
        {
            _release = release;
            Texture = texture;
        }

        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        public Texture2D Texture { get; }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _release();
        }
    }
}