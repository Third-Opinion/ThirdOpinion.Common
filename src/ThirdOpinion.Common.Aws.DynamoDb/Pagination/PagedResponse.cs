namespace ThirdOpinion.Common.Aws.DynamoDb.Pagination;

// Represents a paginated response including both data and metadata
public class PagedResponse<T>
{
    public required List<T> Items { get; set; }
    public required PaginationMetadata Metadata { get; set; }
}

public class PagedNextPageResponse<T>
{
    public required List<T> Items { get; set; }
    public required PaginationNextPageMetaddata Metadata { get; set; }
}