namespace GA.Domain.Core;

public abstract record Asset(string Name, IReadOnlyCollection<object> Items);