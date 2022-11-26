namespace GA.Core.Extensions;

public delegate TPair PairFactory<in T, out TPair>(T item1, T item2) where TPair : IPair<T>;
