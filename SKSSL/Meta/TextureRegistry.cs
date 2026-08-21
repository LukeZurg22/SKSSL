using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using SKSSL.Serializing;
using SKSSL.Textures;

namespace SKSSL;

public partial class TextureRegistry
{
    private readonly ConcurrentDictionary<Handle, TextureType> _handleToType = new();

    /// (Actively Used) Handle -> Material Map Index
    private readonly ConcurrentDictionary<Handle, SerializedMaterial> _materialPaths = new();

    private readonly ConcurrentDictionary<Handle, FileInfo> _iconPaths = new();

    private readonly ConcurrentDictionary<FileInfo, TextureEntry> _activeTextures = new();

    #region Registration

    public void RegisterIcon(Handle handle, FileInfo info)
    {
        // When new textures are being loaded-in, then this will "refresh" them.
        if (_iconPaths.TryGetValue(handle, out FileInfo? oldInfo) && !oldInfo.Equals(info))
            ReleaseTexture(oldInfo);

        // Allows replacement if the handle is the same. Reference need not be included.
        _iconPaths[handle] = info;
        _handleToType[handle] = TextureType.ICON;
    }

    public void RegisterMaterial(Handle handle, TextureMap textureMap, FileInfo info)
    {
        if (_materialPaths.TryGetValue(handle, out SerializedMaterial? value))
        {
            value[textureMap] = info;
            return;
        }

        value = new SerializedMaterial { [textureMap] = info };
        _materialPaths[handle] = value;
        _handleToType[handle] = TextureType.MATERIAL;
    }

    #endregion

    #region Get (The Useful Ones)

    /// <summary>
    /// Hardline Get-method for Icon textures without any risk of getting a Material.
    /// </summary>
    /// <param name="handle"></param>
    /// <param name="mipMap"></param>
    /// <returns></returns>
    // ReSharper disable once UnusedMember.Global
    public TextureLease GetIcon(Handle handle, bool mipMap = false)
        => GetTexture(handle, TextureType.ICON, 0, mipMap);

    /// <summary>
    /// Simple version of the GetTexture method that assumes one is obtaining an Icon, or the Diffuse Map of a Material.
    /// </summary>
    /// <param name="handle"></param>
    /// <param name="mipMap"></param>
    /// <returns></returns>
    // ReSharper disable once UnusedMember.Global
    public TextureLease GetTexture(Handle handle, bool mipMap = false)
    {
        TextureLease texture2D = GetTexture(handle, _handleToType[handle], TextureMap.DIFFUSE, mipMap);
        return texture2D;
    }

    /// <summary>
    /// Acquire an internally stored texture, using the cached instance if necessary.
    /// </summary>
    /// <param name="handle">Handle to texture.</param>
    /// <param name="textureType">Texture's internal type.</param>
    /// <param name="textureMap">Material map, if relevant. Defaults to Diffuse map.</param>
    /// <param name="mipMapped"></param>
    /// <returns></returns>
    /// <remarks>
    /// Assume by this point that mod overrides of game textures are complete.
    /// Use <see cref="GetTexture(SKSSL.Serializing.Handle,bool)"/> instead.
    /// </remarks>
    /// <seealso cref="GetMaterial"/>
    public TextureLease GetTexture(Handle handle, TextureType textureType, TextureMap textureMap, bool mipMapped)
    {
        FileInfo? file = RetrieveTextureFileInfo(handle, textureType, textureMap);
        if (file == null)
            throw new FileNotFoundException($"Texture for '{handle}' could not be found in Registry.");

        // File doesn't exist and failing at the attempt to acquire it means not even the content loader doesn't work.
        if (!File.Exists(file.FullName))
        {
            // If desperate, check game Content Manager. The worst-case scenario is that someone is attempting to
            // retrieve a texture that doesn't exist -~anywhere~-.
            if (TryGetFromContent(handle, mipMapped, out TextureLease? texture))
                return texture;
            throw new FileNotFoundException($"Texture handle \'handle\' for file '{file.FullName}' does not exist.");
            //return new TextureLease(GetErrorTexture(), () => { });
        }

        // Quick-get the texture, or just make an error texture on the spot.
        if (AcquireTexture(file, mipMapped, out TextureLease? lease))
            return lease;
        
        // Log the exception and bootleg together an error texture.
        Log(new FileNotFoundException($"Failed to load texture '{file.FullName}'."));
        return new TextureLease(GetErrorTexture(), () => { });

    }

    private static bool TryGetFromContent(Handle handle, bool mipMapped, [NotNullWhen(true)] out TextureLease? lease)
    {
        lease = null;
        foreach (ContentManager contentManager in SSLGame.Instance.ContentManagers)
        {
            try
            {
                // Disposing this content-loaded texture without using the content manager may be a bad thing?
                // Not sure. The hope is this will work just fine.
                var texture = contentManager.Load<Texture2D>(handle);
                if (mipMapped) texture = texture.ToMipMapped(); // MipMap handling.
                lease = new TextureLease(texture, () => { });

                // COMMENTED THE BELOW OUT AS DISPOSING CONTENT MANAGER TEXTURES MAY BE RISKY.
                //entry = new TextureEntry { Texture = texture, References = 1 };
                //if (_activeTextures.TryAdd(info, entry))
                //{
                //    textureLease = new TextureLease(this, info, texture);
                //    return true;
                //}
                //
                //// Another thread loaded it first.
                //texture.Dispose();
                //entry = _activeTextures[info];
                //Interlocked.Increment(ref entry.References);
                return true;
            }
            catch
            {
                //Log($"Failed to find {info.Name} texture in content manager.", LOG.FILE_ERROR);
            }
        }

        return false;
    }

    /// <summary>
    /// For creating and retrieving individual texture maps for materials.
    /// </summary>
    /// <param name="handle">Handle to the desired texture.</param>
    /// <param name="map">
    /// Type of texture map, which routes the search to looking for a <see cref="SerializedMaterial"/>.
    /// </param>
    /// <param name="mipMapped">Make returned texture mip-mapped.</param>
    /// <returns></returns>
    // ReSharper disable once UnusedMember.Global
    public TextureLease GetTexture(Handle handle, TextureMap map, bool mipMapped = false)
        => GetTexture(handle, TextureType.MATERIAL, map, mipMapped);

    /// <summary>
    /// Loads an entire material's set of maps into memory at once and returns a struct wrapper.
    /// </summary>
    /// <param name="handle">Handle must be "proper" without folder extension.</param>
    /// <returns></returns>
    // ReSharper disable once UnusedMember.Global
    public SKMaterial GetMaterial(Handle handle)
    {
        // Each texture map is cached individually in the active textures dictionary.
        return new SKMaterial
        {
            Diffuse = GetTexture(handle, TextureMap.DIFFUSE),
            Normal = GetTexture(handle, TextureMap.NORMAL),
            Displacement = GetTexture(handle, TextureMap.DISPLACEMENT),
            Emissive = GetTexture(handle, TextureMap.EMISSIVE),
        };
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Attempts to acquire a cached texture, or otherwise loads it from the file.
    /// </summary>
    /// <param name="info"></param>
    /// <param name="mipMapped"></param>
    /// <param name="textureLease"></param>
    /// <returns></returns>
    private bool AcquireTexture(FileInfo info, bool mipMapped, [NotNullWhen(true)] out TextureLease? textureLease)
    {
        textureLease = null!;

        // If the file doesn't exist, oh brother, don't even bother!
        if (!File.Exists(info.FullName))
            return false;

        // Return cached texture in memory.
        if (_activeTextures.TryGetValue(info, out TextureEntry? entry))
        {
            Interlocked.Increment(ref entry.References);
            textureLease = new TextureLease(entry.Texture, () => ReleaseTexture(info));
            return true;
        }

        try // Loading the texture from the file directly, and caching it.
        {
            using var stream = new FileStream(
                info.FullName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.SequentialScan
            );

            Texture2D texture = Texture2D.FromStream(SSLGame.Graphics, stream);
            if (mipMapped) texture = texture.ToMipMapped(); // MipMap handling.
            entry = new TextureEntry { Texture = texture, References = 1 };
            if (!_activeTextures.TryAdd(info, entry))
            {
                // Another thread loaded it first.
                texture.Dispose();
                entry = _activeTextures[info];
                Interlocked.Increment(ref entry.References);
            }

            textureLease = new TextureLease(entry.Texture, () => ReleaseTexture(info));
            return true;
        }
        catch (Exception)
        {
            //Log($"Failed file open-read: {info.Name} :: {ex.Message}", LOG.FILE_ERROR);
        }

        // Plum out of luck. I guess the ERROR texture will have to suffice.
        return false;
    }

    private FileInfo? RetrieveTextureFileInfo(Handle handle, TextureType textureType, TextureMap textureMap)
    {
        FileInfo? textureInfo = null;

        // Depending on the type, get it differently.
        switch (textureType)
        {
            case TextureType.ICON:
                _iconPaths.TryGetValue(handle, out textureInfo);
                break;
            case TextureType.MATERIAL:
                if (_materialPaths.TryGetValue(handle, out SerializedMaterial? material))
                    textureInfo = material[textureMap];
                break;
            case TextureType.TILEMAP:
            default:
                throw new ArgumentOutOfRangeException(
                    $"Texture type \'{textureType}\' is not supported for handle \'{handle}\'.");
        }

        return textureInfo;
    }

    #endregion

    #region De-allocation

    /// <summary>
    /// Removes and disposes a cached texture associated with the specified file.
    /// </summary>
    public bool ReleaseTexture(FileInfo? info)
    {
        if (info == null)
            return false;
        if (!_activeTextures.TryGetValue(info, out TextureEntry? entry))
            return false;
        if (Interlocked.Decrement(ref entry.References) > 0)
            return false;
        if (_activeTextures.TryRemove(new KeyValuePair<FileInfo, TextureEntry>(info, entry)))
            entry.Texture.Dispose();
        return true;
    }

    /// <summary>
    /// Removes and disposes a cached texture associated with the specified handle and provided texture map.
    /// This is capable of selectively releasing segments of cached material maps without completely invalidating
    /// the materials referencing them.
    /// </summary>
    // ReSharper disable once UnusedMember.Global
    public bool ReleaseTexture(Handle handle, TextureMap map)
    {
        TextureType type = _handleToType[handle];
        FileInfo? file = RetrieveTextureFileInfo(handle, type, map);
        return file != null && ReleaseTexture(file);
    }

    /// <summary>
    /// Full dynamic release based on type. Materials are completely released.
    /// </summary>
    /// <param name="handle"></param>
    /// <returns></returns>
    /// <remarks>Not recommended if segments of materials are used dispersed over a program.</remarks>
    // ReSharper disable once UnusedMember.Global
    public bool Release(Handle handle)
    {
        TextureType type = _handleToType[handle];
        switch (type)
        {
            case TextureType.ICON:
                ReleaseTexture(RetrieveTextureFileInfo(handle, type, TextureMap.DIFFUSE));
                break;
            case TextureType.MATERIAL:
                foreach (TextureMap map in Enum.GetValues<TextureMap>())
                    ReleaseTexture(RetrieveTextureFileInfo(handle, type, map));
                break;
            case TextureType.TILEMAP:
            default:
                break;
        }

        return true;
    }

    /// <summary>
    /// Removes and disposes all cached textures.
    /// </summary>
    // ReSharper disable once UnusedMember.Global
    public void DisposeAllTextures()
    {
        foreach (TextureEntry entry in _activeTextures.Values)
            entry.Texture.Dispose();
        _activeTextures.Clear();
    }

    #endregion

    private sealed class TextureEntry
    {
        public required Texture2D Texture { get; init; }
        public int References;
    }

    private const int ERROR_DEFAULT_WIDTH = 128;
    private const int ERROR_DEFAULT_HEIGHT = 128;
    private static Texture2D? DefaultError;

    /// <returns>Cached Default Error Texture, or creates a new one if one is not present. Defaults to 128x128.</returns>
    public static Texture2D GetErrorTexture(int width = ERROR_DEFAULT_WIDTH, int height = ERROR_DEFAULT_HEIGHT)
    {
        if (DefaultError != null)
            return DefaultError;

        var texture = new Texture2D(SSLGame.Graphics, width, height);
        var pixels = new Color[128 * 128];

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            bool checker = (x / 32 + y / 32) % 2 == 0;
            pixels[y * 128 + x] = checker ? new Color(1f, 0f, 1f, 1f) : Color.Black; // Magenta / Black
        }

        texture.SetData(pixels);
        DefaultError = texture;
        return texture;
    }
}