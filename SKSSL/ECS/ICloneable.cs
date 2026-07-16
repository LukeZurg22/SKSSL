namespace SKSSL.ECS;

/// <summary>
/// Interface for an object type whose properties and fields may be directly copied onto a duplicate instance. 
/// </summary>
/// <typeparam name="T">Generic type expected in supertype.</typeparam>
public interface ICloneable<out T>
{
    public T Clone();
}