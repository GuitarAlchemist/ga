namespace GA.Business.Core;

public interface IMusicObjectCollection<out TSelf>
{
    // ReSharper disable once InconsistentNaming
    public static abstract IEnumerable<TSelf> Objects { get; }
}