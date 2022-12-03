namespace GA.Core.Collections;

public class LazyCollection<T> : LazyCollectionBase<T> 
    where T : class
{
    public LazyCollection(IEnumerable<T> items) 
        : base(items)
    {
    }
}