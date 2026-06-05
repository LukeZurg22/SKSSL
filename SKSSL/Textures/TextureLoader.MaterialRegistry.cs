using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using SKSSL.Extensions;

namespace SKSSL.Textures;

public abstract partial class TextureLoader
{
    private static void LoadTextureMaterial(string file, string folderName)
    {
        var materialGroups = new Dictionary<string, SKMaterial>(StringComparer.OrdinalIgnoreCase);

        string fileNameNoExt = Path.GetFileNameWithoutExtension(file);
        string baseName = fileNameNoExt.RemoveUnderscoreEndingTag();
        string suffix = fileNameNoExt.GetUnderscoreEndingTag();

        if (!Enum.TryParse(suffix, true, out TextureType textureType))
            textureType = TextureType.DIFFUSE;

        // Unique material key (folderPrefix + baseName) -> allows overrides from mods
        string materialKey = $"{folderName}_{baseName}";

        if (!materialGroups.TryGetValue(materialKey, out SKMaterial? material))
        {
            material = new SKMaterial();
            materialGroups[materialKey] = material;
        }

        Texture2D texture = LoadFromFileOrContent(file, materialKey, folderName);

        // Assign map (later files override earlier ones for the same material + type)
        switch (textureType)
        {
            case TextureType.DIFFUSE: material.Diffuse = texture; break;
            case TextureType.NORMAL: material.Normal = texture; break;
            case TextureType.DISPLACEMENT: material.Displacement = texture; break;
            case TextureType.EMISSIVE: material.Emissive = texture; break;
            default: throw new ArgumentOutOfRangeException { Source = nameof(TextureLoader) };
        }

        // Register / override materials in the MaterialRegistry
        foreach (var pair in materialGroups)
        {
            MaterialRegistry.RegisterMaterial(pair.Key, pair.Value); // This should internally support override
        }
    }

    /// <summary>
    /// Internal Material Registry for Texture Loader class. Utilized for any kind of object that requires more than one map.
    /// Handles multiple map-types.
    /// </summary>
    public static class MaterialRegistry
    {
        /// The maximum number of materials the game is willing to load at any given runtime instance.
        private const int MaxMaterials = 2048;

        /// Used as numerical ID selector for new materials, as well as total material counter. 
        public static int MaterialCount { get; private set; } = 0;

        /// Materials used by the game. <seealso cref="SKMaterial"/>
        public static readonly SKMaterial[] Materials = new SKMaterial[MaxMaterials];

        /// Only used during loading. Assigns a material name to a <see cref="SKMaterial"/>'s integer ID.
        public static readonly Dictionary<string, int> NameToId = new(MaxMaterials);

        ///  Storing Content handles to content paths, the former of which are generated from their paths.
        private static readonly Dictionary<string, string> _handleToContentPath =
            new(StringComparer.OrdinalIgnoreCase);

        private static bool _contentIndexBuilt = false;

        // WIP: Move BuildContentIndex somewhere else. Per-Game-Folder handling is real, NOW!
        
        /// <summary>
        /// Scans all ContentManagers' output directories and builds a map from material handle 
        /// (e.g. "gneiss_fun_test_three_diffuse") to the correct asset path for Content.Load.
        /// Call this once after all ContentManagers are ready (e.g. in TextureLoader.Initialize).
        /// </summary>
        public static void BuildContentIndex() 
        {
            if (_contentIndexBuilt) return;
            _contentIndexBuilt = true;

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

            Log($"Content index built. Found {_handleToContentPath.Count} assets.");
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

                _handleToContentPath[$"{folderPrefix}_{fileNameNoExt}"] = relativePath;
            }

            // Recurse into subdirectories
            foreach (var subDir in Directory.GetDirectories(currentDir))
                IndexDirectory(rootDir, subDir);
            return;

            // Lightweight check to determine if an .xnb file contains a Texture2D.
            // This avoids trying to Load<Texture2D> on non-image content (which would throw or waste time).
            static bool IsXnbTexture2D(string xnbFilePath)
            {
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


        /// <summary>
        /// Registers or gets an existing material ID by name.
        /// Called during loading when a multi-texture folder is processed.
        /// </summary>
        public static int RegisterMaterial(string name, SKMaterial material)
        {
            // Override existing material if it already exists (important for mods)
            if (NameToId.TryGetValue(name, out int existingId))
            {
                Materials[existingId] = material; // Replace the actual material data
                Log($"...material ({existingId}) overridden by mod: {name}...", LOG.FILE_WARNING);
                return existingId;
            }

            if (MaterialCount >= MaxMaterials)
                throw new InvalidOperationException($"Exceeded maximum material limit ({MaxMaterials})");

            int newId = MaterialCount++;
            Materials[newId] = material;
            NameToId[name] = newId;
            return newId;
        }

        /// <summary>
        /// Default material with error and null texture mappings.
        /// </summary>
        private static readonly SKMaterial DefaultErrorMaterial = SKMaterial.Error(HardcodedTextures.GetErrorTexture());

        #region Get Methods

        /// <summary>
        /// Fast access by ID — used heavily at runtime.
        /// <returns>
        /// <see cref="SKMaterial"/> instance, which will be the Default Error Material if the id is &lt;0.
        /// </returns>
        /// <remarks>
        /// If id &lt; 0, or id &gt; Material Count, use Default Error Material.
        /// Otherwise, utilize Materials[id] entry.
        /// </remarks>
        /// </summary>
        public static SKMaterial GetMaterial(int id)
            => id < 0 || id >= MaterialCount ? DefaultErrorMaterial : Materials[id];

        /// <returns>
        /// Returns the ID for a material name. If not registered, it will attempt to load it from MonoGame Content
        /// (.xnb) as a fallback.
        /// </returns>
        private static int GetId(string handle)
        {
            if (string.IsNullOrWhiteSpace(handle))
                return -1;

            // Fast path: already registered (base game or mod)
            if (NameToId.TryGetValue(handle, out int id))
                return id;

            // Ensure index is built (safe to call multiple times)
            if (!_contentIndexBuilt)
                BuildContentIndex();

            // === Fallback: Try to load from MonoGame Content pipeline ===
            //Log($"Material '{handle}' not registered. Attempting Content pipeline fallback...", LOG.FILE_WARNING);

            // Try exact match from scanned content
            if (_handleToContentPath.TryGetValue(handle, out string? assetPath))
            {
                Texture2D? diffuse = null;

                foreach (ContentManager contentManager in SSLGame.Instance.ContentManagers)
                {
                    try
                    {
                        diffuse = contentManager.Load<Texture2D>(assetPath).ToMipMapped();
                        //Log($"Content fallback succeeded for '{handle}' using real path: {assetPath}");
                        // TODO: Include "unimportant" or "content override" log.
                        break;
                    }
                    catch (ContentLoadException)
                    {
                    }
                    catch (Exception ex)
                    {
                        Log($"Error loading '{assetPath}': {ex.Message}", LOG.FILE_WARNING);
                    }
                }

                if (diffuse != null)
                {
                    var fallbackMaterial = new SKMaterial { Diffuse = diffuse };
                    // TODO: Dynamically search-back for full & complete material files. Implement in IndexDirectory?
                    return RegisterMaterial(handle, fallbackMaterial);
                }
            }

            // Final fallback
            //Log($"Material '{handle}' not found in registry or content pipeline. Using error material.", LOG.FILE_WARNING);
            return -1;
        }

        /// <summary>
        /// Overload for <see cref="GetMaterial(int)"/> that attempts to try-get value.
        /// </summary>
        /// <param name="handle"><see cref="string"/> reference id name for material.</param>
        /// <returns>Material by reference id, or <see cref="DefaultErrorMaterial"/></returns>
        /// <remarks>Typically reference is "folder_..._folder_texture"</remarks>
        /// <example>GetMaterial("gneiss_rock");</example>
        [Pure]
        public static SKMaterial GetMaterial(string handle)
        {
            if (string.IsNullOrWhiteSpace(handle))
                return DefaultErrorMaterial;

            int id = GetId(handle);
            return GetMaterial(id);
        }

        #endregion
    }
}