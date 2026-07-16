namespace SKSSL.ECS;

public interface ICloneable<out T>
{
    public T Clone();
}