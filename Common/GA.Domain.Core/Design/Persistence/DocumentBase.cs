namespace GA.Domain.Core.Design.Persistence;

using System;
using System.Collections.Generic;

/// <summary>
/// Base class for all domain documents that can be persisted.
/// </summary>
public abstract record DocumentBase
{
    public string Id { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = [];
}


