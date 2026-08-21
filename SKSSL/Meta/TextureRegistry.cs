using System;
using System.Collections.Concurrent;
using System.IO;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using SKSSL.Serializing;
using SKSSL.Textures;

namespace SKSSL;

public class TextureRegistry
{
    private readonly ConcurrentDictionary<Handle, TextureType> _handleToType = new();

    /// (Actively Used) Handle -> Material Map Index
    private readonly ConcurrentDictionary<Handle, SerializedMaterial> _materialPaths = new();

    private readonly ConcurrentDictionary<Handle, FileInfo> _iconPaths = new();

    private readonly ConcurrentDictionary<FileInfo, Texture2D> _activeTextures = new();

    #region Registration

    public void RegisterIcon(Handle handle, FileInfo info)
    {
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

    /// <inheritdoc cref="GetTexture(SKSSL.Serializing.Handle, SKSSL.TextureType, SKSSL.TextureMap)"/>
    // ReSharper disable once UnusedMember.Global
    public Texture2D GetTexture(Handle handle, bool mipMapped = false)
    {
        Texture2D texture2D = GetTexture(handle, _handleToType[handle], TextureMap.DIFFUSE);
        return mipMapped ? texture2D.ToMipMapped() : texture2D;
    }

    /// <summary>
    /// Acquire an internally stored texture, using the cached instance if necessary.
    /// </summary>
    /// <param name="handle">Handle to texture.</param>
    /// <param name="textureType">Texture's internal type.</param>
    /// <param name="textureMap">Material map, if relevant. Defaults to Diffuse map.</param>
    /// <returns></returns>
    /// <remarks>Assume by this point that mod overrides of game textures are complete.</remarks>
    /// <seealso cref="GetMaterial"/>
    private Texture2D GetTexture(Handle handle, TextureType textureType, TextureMap textureMap)
    {
        FileInfo? file = RetrieveTextureFileInfo(handle, textureType, textureMap);
        if (file != null)
        {
            // Actually load texture and cache it.
            return AcquireTexture(handle, file, out Texture2D texture2D)
                ? texture2D
                : TextureLoader.GetErrorTexture();
        }

        // Check cached.
        switch (textureType)
        {
            case TextureType.ICON:
                Log($"Icon for \'{handle}\' not found!", LOG.FILE_WARNING);
                break;
            case TextureType.MATERIAL:
                Log($"Material map {textureMap} for \'{handle}\' not found!", LOG.FILE_WARNING);
                break;
        }

        // Default to ERROR texture and call it a day.
        return TextureLoader.GetErrorTexture();
    }

    /// <summary>
    /// Attempts to acquire a cached texture, or otherwise loads it from the file.
    /// </summary>
    /// <param name="handle"></param>
    /// <param name="info"></param>
    /// <param name="texture"></param>
    /// <returns></returns>
    private bool AcquireTexture(Handle handle, FileInfo info, out Texture2D texture)
    {
        texture = TextureLoader.GetErrorTexture();
        // If the file doesn't exist, oh brother, don't even bother!
        if (!File.Exists(info.FullName))
            return false;

        // Return cached texture in memory.
        if (_activeTextures.TryGetValue(info, out texture!))
            return true;

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
            texture = Texture2D.FromStream(SSLGame.Graphics, stream);
            _activeTextures[info] = texture;
            return true;
        }
        catch (Exception ex)
        {
            Log($"Failed file open-read: {info.Name} :: {ex.Message}", LOG.FILE_ERROR);
        }

        // If desperate, check game content?
        foreach (ContentManager contentManager in SSLGame.Instance.ContentManagers)
        {
            try
            {
                texture = contentManager.Load<Texture2D>(handle);
                return true;
            }
            catch
            {
                Log($"Failed to find {info.Name} texture in content manager.", LOG.FILE_ERROR);
            }
        }

        // Plum out of luck. I guess the ERROR texture will have to suffice.
        return false;
    }

    /// <summary>
    /// For creating and retrieving individual texture maps for materials.
    /// </summary>
    /// <param name="handle"></param>
    /// <param name="map"></param>
    /// <returns></returns>
    // ReSharper disable once UnusedMember.Global
    public Texture2D GetTexture(Handle handle, TextureMap map) => GetTexture(handle, TextureType.MATERIAL, map);

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

    /// <summary>
    /// Loads an entire material's set of maps into memory at once and returns a struct wrapper.
    /// </summary>
    /// <param name="handle">Handle must be "proper" without folder extension.</param>
    /// <returns></returns>
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
}