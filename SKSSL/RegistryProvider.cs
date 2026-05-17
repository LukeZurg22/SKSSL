namespace SKSSL;

/// <summary>
/// Service-provider registry used in various parts of a program by classes that declare the usage of this service. 
/// </summary>
/// <typeparam name="T"></typeparam>
public class RegistryProvider<T> : IServiceProvider where T : class, new()
{
    /// <summary>
    /// Active registry used.
    /// </summary>
    public T? Registry { get; private set; } = new();

    /// <summary>
    /// 
    /// </summary>
    public void Replace(T replacement) => Registry = replacement;

    /// <inheritdoc />
    public object? GetService(Type serviceType)
    {
        return serviceType == typeof(T) ? Registry : null;
    }
}