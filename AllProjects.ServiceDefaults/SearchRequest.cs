namespace AllProjects.ServiceDefaults;

using System.Collections.Generic;

/// <summary>
///     Search request parameters
/// </summary>
public class SearchRequest : PaginationRequest
{
    /// <summary>
    ///     Search query string
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    ///     Filters to apply
    /// </summary>
    public Dictionary<string, string>? Filters { get; set; }

    /// <summary>
    ///     Sort field
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>
    ///     Sort direction (asc/desc)
    /// </summary>
    public string SortDirection { get; set; } = "asc";

    /// <summary>
    ///     Whether to include fuzzy matching
    /// </summary>
    public bool FuzzySearch { get; set; } = false;
}
