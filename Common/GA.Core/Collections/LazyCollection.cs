namespace GA.Core.Collections;

public class LazyCollection<T>(IEnumerable<T> items) : LazyCollectionBase<T>(items)
    where T : class;