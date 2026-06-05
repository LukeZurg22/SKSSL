using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework.Content;
using SKSSL.Extensions;

namespace SKSSL.Textures;

public abstract partial class TextureLoader
{
    public static class XNAContentLoader
    {
        ///  Storing Content handles to content paths, the former of which are generated from their paths.
        internal static readonly Dictionary<string, string> HandleToContentPath =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Scans all ContentManagers' output directories and builds a map from material handle 
        /// (e.g. "gneiss_fun_test_three_diffuse") to the correct asset path for Content.Load.
        /// Call this once after all ContentManagers are ready (e.g. in TextureLoader.Initialize).
        /// </summary>
        public static void BuildContentIndex()
        {
            Log("Building content index.");

            foreach (ContentManager contentManager in SSLGame.Instance.ContentManagers)
            {
                if (string.IsNullOrEmpty(contentManager.RootDirectory) ||
                    !Directory.Exists(contentManager.RootDirectory))
                    continue;

                try
                {
                    IndexDirectory(contentManager.RootDirectory, contentManager.RootDirectory);
                }
                catch (Exception ex)
                {
                    Log($"Failed to index Content directory '{contentManager.RootDirectory}': {ex.Message}",
                        LOG.FILE_WARNING);
                }
            }

            Log($"Content index built. Found {HandleToContentPath.Count} assets.");
        }

        /// <summary>
        /// Recursively indexes .xnb files and maps the filename (without extension) to its relative asset path.
        /// </summary>
        private static void IndexDirectory(string rootDir, string currentDir)
        {
            // Gum is a reserved keyword, now, I guess! Seriously. GUM UI elements should not be loaded as material
            //  textures. This -must- cut short if GUM UI is read.
            if (currentDir.Contains("Gum"))
                return;

            string folderPrefix = Path.GetFileName(currentDir).ToLowerInvariant();

            // Index .xnb files in current directory
            foreach (var file in Directory.GetFiles(currentDir, "*.xnb", SearchOption.TopDirectoryOnly))
            {
                // Hard-coded Texture Type values are avoided like the plague.
                string fileNameNoExt = Path.GetFileNameWithoutExtension(file);
                string suffix = fileNameNoExt.GetUnderscoreEndingTag();
                if (!string.IsNullOrEmpty(suffix) && Enum.TryParse(suffix, true, out TextureType _))
                    continue;

                // Quick header check: Is this actually a Texture2D?
                if (!IsXnbTexture2D(file))
                    continue; // Skip models, sounds, SpriteFont, Effect, etc.

                string relativePath =
                    Path.GetRelativePath(rootDir, Path.ChangeExtension(file, null)).Replace('\\', '/');

                HandleToContentPath[$"{folderPrefix}_{fileNameNoExt}"] = relativePath;
            }

            // Recurse into subdirectories
            foreach (var subDir in Directory.GetDirectories(currentDir))
                IndexDirectory(rootDir, subDir);
            return;

            // Lightweight check to determine if an .xnb file contains a Texture2D.
            // This avoids trying to Load<Texture2D> on non-image content (which would throw or waste time).
            static bool IsXnbTexture2D(string xnbFilePath)
            {
                // Try-Catch disregards the performance benefits of the Content loader's purpose, but it is needed
                //  for the intended dynamacy. Additional series' of checks could be provided, but the number of
                //  potential exceptions, and the nature of those exceptions makes it seem as so I cannot accomodate
                //  for them all.
                try
                {
                    using FileStream fs = File.OpenRead(xnbFilePath);
                    using var br = new BinaryReader(fs);

                    // XNB header: "XNB" + platform + version + flags
                    var XNBdHeader = br.ReadInt32();
                    if (XNBdHeader !=
                        0x64424E58) // "XNB" in little-endian (actual magic is XNBw/x/z etc. checking bytes)
                        return false; // Not even a valid XNB

                    // Skip platform byte, version, flags, etc.
                    br.ReadString(); // skip string's length
                    br.ReadString(); // skip again
                    var typeReaderName = br.ReadString(); // Reading butchered string, but should have what's needed.
                    return typeReaderName.Contains("Texture2DReader", StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    // If anything fails (corrupt file, etc.), treat as non-texture
                    return false;
                }
            }
        }
    }
}