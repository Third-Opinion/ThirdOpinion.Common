namespace ThirdOpinion.Common.Aws.DynamoDb.Pagination;

/// <summary>
///     Contains metadata information for paginated responses with page-based navigation
/// </summary>
public class PaginationMetadata
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="PaginationMetadata" /> class
    /// </summary>
    /// <param name="currentPage">The current page number</param>
    /// <param name="pageSize">The number of items per page</param>
    /// <param name="totalCount">The total number of items across all pages</param>
    public PaginationMetadata(int currentPage, int pageSize, int? totalCount)
    {
        CurrentPage = currentPage;
        PageSize = pageSize;
        TotalCount = totalCount;
        if (totalCount.HasValue)
            TotalPages = (int)Math.Ceiling(totalCount.Value / (double)pageSize);
    }

    /// <summary>
    ///     Gets or sets the current page number
    /// </summary>
    public int CurrentPage { get; set; }

    /// <summary>
    ///     Gets or sets the total number of pages
    /// </summary>
    public int? TotalPages { get; set; }

    /// <summary>
    ///     Gets or sets the number of items per page
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    ///     Gets or sets the total number of items across all pages
    /// </summary>
    public int? TotalCount { get; set; }

    /// <summary>
    ///     Gets a value indicating whether there is a previous page available
    /// </summary>
    public bool HasPrevious => CurrentPage > 1;

    /// <summary>
    ///     Gets a value indicating whether there is a next page available
    /// </summary>
    public bool HasNext => CurrentPage < TotalPages;

    /// <summary>
    ///     Gets or sets the navigation links for pagination
    /// </summary>
    public Dictionary<string, Uri> Links { get; set; } = [];
}

/// <summary>
///     Contains metadata information for paginated responses with token-based navigation
/// </summary>
public class PaginationNextPageMetaddata
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="PaginationNextPageMetaddata" /> class
    /// </summary>
    /// <param name="pageSize">The number of items per page</param>
    /// <param name="nextPageToken">The token for accessing the next page</param>
    public PaginationNextPageMetaddata(int pageSize, string? nextPageToken = null)
    {
        PageSize = pageSize;
        NextPageToken = nextPageToken;
    }

    /// <summary>
    ///     Gets or sets the token for accessing the next page
    /// </summary>
    public string? NextPageToken { get; set; }

    /// <summary>
    ///     Gets or sets the number of items per page
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    ///     Gets or sets the navigation links for pagination
    /// </summary>
    public Dictionary<string, Uri> Links { get; set; } = [];
}