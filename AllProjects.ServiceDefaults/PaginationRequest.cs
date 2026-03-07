namespace AllProjects.ServiceDefaults;

/// <summary>
///     Request parameters for paginated endpoints
/// </summary>
public class PaginationRequest
{
    /// <summary>
    ///     Page number (1-based, default: 1)
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    ///     Number of items per page (default: 50, max: 1000)
    /// </summary>
    public int PageSize { get; set; } = 50;

    /// <summary>
    ///     Calculate skip count for database queries
    /// </summary>
    public int Skip => (Page - 1) * PageSize;

    /// <summary>
    ///     Validate pagination parameters
    /// </summary>
    public void Validate()
    {
        if (Page < 1)
        {
            Page = 1;
        }

        if (PageSize < 1)
        {
            PageSize = 50;
        }

        if (PageSize > 1000)
        {
            PageSize = 1000;
        }
    }
}
