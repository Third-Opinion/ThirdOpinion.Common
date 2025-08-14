namespace ThirdOpinion.Common.Aws.DynamoDb.Pagination;

/// <summary>
///     Query parameters for pagination
/// </summary>
public class PaginationQuery
{
    private readonly int _pageSize = 10;

    public PaginationQuery(int pageNumber, int pageSize = 10, int maxPageSize = 100)
    {
        MaxPageSize = maxPageSize;
        pageNumber = pageNumber;
    }

    public PaginationQuery()
    {
    }

    public int MaxPageSize { get; } = 100;

    public int PageNumber { get; set; } = 1;

    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = Math.Min(value, MaxPageSize);
    }
}

public class PaginationNextQuery
{
    private readonly int _pageSize = 10;

    public PaginationNextQuery(int pageSize = 10,
        int maxPageSize = 100,
        string? nextPageToken = null)
    {
        MaxPageSize = maxPageSize;
        PageSize = pageSize;
        NextPageToken = nextPageToken;
    }

    public PaginationNextQuery()
    {
    }

    public string? NextPageToken { get; set; }

    public int MaxPageSize { get; } = 100;

    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = Math.Min(value, MaxPageSize);
    }
}