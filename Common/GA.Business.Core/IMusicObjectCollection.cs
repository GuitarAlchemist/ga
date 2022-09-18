namespace GA.Business.Core;

public interface IMusicObjectCollection<out T>
{
    public static abstract IEnumerable<T> Objects { get; }
}