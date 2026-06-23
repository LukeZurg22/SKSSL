using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace SKSSL.Textures;

public partial class TextureLoader
{
    /// <remarks>
    /// Icons have additional caching and handling than materials, which are uniquely handled in this method. 
    /// </remarks>
    private static void LoadTextureIcon(string file, string sanitizedFolderName)
    {
        // Use category + key to avoid cross-category collisions, but still allow overrides within category
        string fullKey = $"{sanitizedFolderName}/{Path.GetFileNameWithoutExtension(file).ToLowerInvariant()}";
        bool validFolderName = !string.IsNullOrEmpty(sanitizedFolderName);

        // Pre-arrange cached icon dictionary if it is not present.
        if (validFolderName && !_textures.TryGetValue(sanitizedFolderName, out var categoryDictionary))
        {
            categoryDictionary = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
            _textures[sanitizedFolderName] = categoryDictionary;
        }

        Texture2D texture = LoadFromFileOrContent(file, fullKey, sanitizedFolderName);

        // Cache icon for future use. This assignment automatically overrides any previous texture with the same key
        if (validFolderName) _textures[sanitizedFolderName][fullKey] = texture;
    }
}