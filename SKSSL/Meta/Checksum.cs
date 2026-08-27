using System;
using System.Security.Cryptography;
using System.Text;
using SKSSL.ECS.Registry;

// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global

namespace SKSSL;

/// <summary>
/// Creates a checksum based on game content & engine state.
/// </summary>
public class Checksum
{
    public readonly string Value;

    private Checksum(string toHexString) => Value = toHexString;

    // TODO: Improve this checksum algorithm to be more content-centric, rather than rely on counting & engine config,
    //  which generally doesn't prevent unwanted external modifications.
    /// <param name="game">Game instance to help build checksum.</param>
    /// <returns>String checksum based on engine-loaded content.</returns>
    /// <remarks>Not very algorithmically effective. Clash-cases can happen</remarks>
    public static Checksum Generate(SSLGame game)
    {
        int totalRegistryCount = MasterRegistryManager.Count();
        byte[] regBytes = Encoding.UTF8.GetBytes($"ENGINE_CONFIG_HASH:{game.Config.GetHashCode()};");
        byte[] configBytes = Encoding.UTF8.GetBytes(SSLGame.Engine.ToString());
        byte[] texBytes = Encoding.UTF8.GetBytes($"$TEXTURES:{XNAContentLoader.HandleToContentPath.Count}");

        byte[] data = new byte[
            regBytes.Length +
            configBytes.Length +
            texBytes.Length
        ];

        int offset = 0;
        Buffer.BlockCopy(regBytes, 0, data, offset, regBytes.Length);
        offset += regBytes.Length;

        Buffer.BlockCopy(configBytes, 0, data, offset, configBytes.Length);
        offset += configBytes.Length;

        Buffer.BlockCopy(texBytes, 0, data, offset, texBytes.Length);

        byte[] hash = SHA256.HashData(data);

        return new Checksum(Convert.ToHexString(hash));
    }
}