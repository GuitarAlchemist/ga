namespace GA.Business.Assets;

public sealed record Asset<T>(string Name, IReadOnlyCollection<T> TypedItems)
    : Asset(Name, [.. TypedItems.Cast<object>()]) where T : notnull;
