namespace GA.Domain.Core;

using System.Collections.Generic;
using System.Linq;

public sealed record Asset<T>(string Name, IReadOnlyCollection<T> TypedItems)
    : Asset(Name, [.. TypedItems.Cast<object>()]) where T : notnull;
