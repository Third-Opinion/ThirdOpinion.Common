namespace ThirdOpinion.Common.Aws.DynamoDb.Pagination;

/// <summary>
///     Represents a paginated response including both data and metadata
/// </summary>
/// <typeparam name="T">The type of items in the paginated response</typeparam>
public class PagedResponse<T>
{
    /// <summary>
    ///     Gets or sets the list of items in the current page
    /// </summary>
    public required List<T> Items { get; set; }

    /// <summary>
    ///     Gets or sets the pagination metadata for the current page
    /// </summary>
    public required PaginationMetadata Metadata { get; set; }
}

/// <summary>
///     Represents a paginated response for next page navigation including both data and metadata
/// </summary>
/// <typeparam name="T">The type of items in the paginated response</typeparam>
public class PagedNextPageResponse<T>
{
    /// <summary>
    ///     Gets or sets the list of items in the current page
    /// </summary>
    public required List<T> Items { get; set; }

    /// <summary>
    ///     Gets or sets the pagination metadata for next page navigation
    /// </summary>
    public required PaginationNextPageMetaddata Metadata { get; set; }
}