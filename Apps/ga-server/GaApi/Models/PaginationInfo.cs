namespace GaApi.Models;

/// <summary>
///     Pagination information for paginated responses
/// </summary>
public class PaginationInfo
{
    /// <summary>
    ///     Current page number (1-based)
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    ///     Number of items per page
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    ///     Total number of items
    /// </summary>
    public long TotalItems { get; set; }

    /// <summary>
    ///     Total number of pages
    /// </summary>
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);

    /// <summary>
    ///     Whether there is a next page
    /// </summary>
    public bool HasNext => Page < TotalPages;

    /// <summary>
    ///     Whether there is a previous page
    /// </summary>
    public bool HasPrevious => Page > 1;

    /// <summary>
    ///     Number of items in the current page
    /// </summary>
    public int ItemCount { get; set; }
}