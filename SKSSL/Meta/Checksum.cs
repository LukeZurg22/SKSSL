using System;
using System.Security.Cryptography;
using System.Text;
using SKSSL.ECS.Registry;
using SKSSL.Textures;
// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global

namespace SKSSL;

/// <summary>
/// Creates a checksum based on game content & engine state.
/// </summary>
public class Checksum
{
    // TODO: Improve this checksum algorithm to be more content-centric, rather than rely on counting & engine config,
    //  which generally doesn't prevent unwanted external modifications.
    /// <returns>String checksum based on engine-loaded content.</returns>
    /// <remarks>Not very algorithmically effective. Clash-cases can happen</remarks>
    public static string Generate()
    {
        int totalRegistryCount = MasterRegistryManager.Count();
        byte[] regBytes = Encoding.UTF8.GetBytes($"REG.COUNT:{totalRegistryCount};");
        byte[] configBytes = Encoding.UTF8.GetBytes(SSLGame.Config.ToString());
        byte[] texBytes =
            Encoding.UTF8.GetBytes($"$TEXTURES:{TextureLoader.XNAContentLoader.HandleToContentPath.Count}");

        byte[] data = new byte[regBytes.Length + configBytes.Length + texBytes.Length];
        // Reg
        Buffer.BlockCopy(regBytes, 0, data, 0, regBytes.Length);
        // Config
        Buffer.BlockCopy(regBytes, 0, data, texBytes.Length, configBytes.Length);
        // Tex
        Buffer.BlockCopy(regBytes, 0, data, texBytes.Length + configBytes.Length, texBytes.Length);

        byte[] hash = SHA256.HashData(data);

        // Convert to hexadecimal
        return Convert.ToHexString(hash);
    }
}