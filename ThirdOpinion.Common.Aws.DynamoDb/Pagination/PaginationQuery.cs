namespace ThirdOpinion.Common.Aws.DynamoDb.Pagination;

/// <summary>
///     Query parameters for pagination
/// </summary>
public class PaginationQuery
{
    private int _pageSize = 10;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PaginationQuery" /> class
    /// </summary>
    /// <param name="pageNumber">The page number to retrieve</param>
    /// <param name="pageSize">The number of items per page</param>
    /// <param name="maxPageSize">The maximum allowed page size</param>
    public PaginationQuery(int pageNumber, int pageSize = 10, int maxPageSize = 100)
    {
        MaxPageSize = maxPageSize;
        PageNumber = pageNumber;
        PageSize = pageSize;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="PaginationQuery" /> class with default values
    /// </summary>
    public PaginationQuery()
    {
    }

    /// <summary>
    ///     Gets the maximum allowed page size
    /// </summary>
    public int MaxPageSize { get; init; } = 100;

    /// <summary>
    ///     Gets or sets the page number to retrieve
    /// </summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>
    ///     Gets or sets the number of items per page, capped at MaxPageSize
    /// </summary>
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = Math.Min(value, MaxPageSize);
    }
}

/// <summary>
///     Query parameters for token-based pagination
/// </summary>
public class PaginationNextQuery
{
    private readonly int _pageSize = 10;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PaginationNextQuery" /> class
    /// </summary>
    /// <param name="pageSize">The number of items per page</param>
    /// <param name="maxPageSize">The maximum allowed page size</param>
    /// <param name="nextPageToken">The token for accessing the next page</param>
    public PaginationNextQuery(int pageSize = 10,
        int maxPageSize = 100,
        string? nextPageToken = null)
    {
        MaxPageSize = maxPageSize;
        PageSize = pageSize;
        NextPageToken = nextPageToken;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="PaginationNextQuery" /> class with default values
    /// </summary>
    public PaginationNextQuery()
    {
    }

    /// <summary>
    ///     Gets or sets the token for accessing the next page
    /// </summary>
    public string? NextPageToken { get; set; }

    /// <summary>
    ///     Gets the maximum allowed page size
    /// </summary>
    public int MaxPageSize { get; } = 100;

    /// <summary>
    ///     Gets or sets the number of items per page, capped at MaxPageSize
    /// </summary>
    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = Math.Min(value, MaxPageSize);
    }
}