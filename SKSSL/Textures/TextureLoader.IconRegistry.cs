using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace SKSSL.Textures;

public abstract partial class TextureLoader
{
    
    /// <remarks>
    /// Icons have additional caching and handling than materials, which are uniquely handled in this method. 
    /// </remarks>
    private static void LoadTextureIcon(string file, string sanitizedFolderName)
    {
        string baseKey = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();

        // Use category + key to avoid cross-category collisions, but still allow overrides within category
        string fullKey = $"{sanitizedFolderName}/{baseKey}";

        // Pre-arrange cached icon dictionary
        if (!string.IsNullOrEmpty(sanitizedFolderName) &&
            !_textures.TryGetValue(sanitizedFolderName, out var categoryDictionary))
        {
            categoryDictionary = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
            _textures[sanitizedFolderName] = categoryDictionary;
        }

        Texture2D texture = LoadFromFileOrContent(file, fullKey, sanitizedFolderName);

        // Cache icon for future use.
        if (!string.IsNullOrEmpty(sanitizedFolderName))
            _textures[sanitizedFolderName][fullKey] = texture;

        // This assignment automatically overrides any previous texture with the same key
        _textures[sanitizedFolderName][fullKey] = texture;
    }
}