namespace GA.Business.Assets;

public abstract record Asset(string Name, IReadOnlyCollection<object> Items);
