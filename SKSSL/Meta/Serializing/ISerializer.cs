using System;
using System.Collections.Generic;
using SKSSL.ECS;

namespace SKSSL.Serializing;

/// <summary>
/// SKSSL interface for developer-inserted serializers into game loaders.
/// </summary>
public interface ISerializer
{
    /// <summary>
    /// The developer-implemented interim layer that handles the [de]serialization logic.
    /// </summary>
    /// <param name="obj">Some object which may or may not be a list.</param>
    /// <typeparam name="T">The type the object is supposed to represent, or be a list of represented.</typeparam>
    /// <returns>A serialized string of the object.</returns>
    public string Serialize<T>(T obj) where T : class;
    
    /// <summary>
    /// User-Developer implementation for deserialization logic for game content.
    /// </summary>
    /// <param name="text">Text to deserialize.</param>
    /// <param name="trace">Filepath passthrough for tracing.</param>
    /// <param name="types">Explicit types for conversions. (Optional)</param>
    /// <returns></returns>
    public List<Prototype> Deserialize(string text, string trace = "", params Type[] types);

}