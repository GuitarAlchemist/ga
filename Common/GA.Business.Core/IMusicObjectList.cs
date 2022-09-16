namespace GA.Business.Core;

public interface IMusicObjectList<TSelf> : IMusicObjectCollection<TSelf>
{
    public new static abstract IList<TSelf> Objects { get; }
}