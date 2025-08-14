namespace ThirdOpinion.Common.Aws.DynamoDb.Pagination;

public class PaginationMetadata
{
    public PaginationMetadata(int currentPage, int pageSize, int? totalCount)
    {
        CurrentPage = currentPage;
        PageSize = pageSize;
        TotalCount = totalCount;
        if (totalCount.HasValue)
            TotalPages = (int)Math.Ceiling(totalCount.Value / (double)pageSize);
    }

    public int CurrentPage { get; set; }
    public int? TotalPages { get; set; }
    public int PageSize { get; set; }
    public int? TotalCount { get; set; }
    public bool HasPrevious => CurrentPage > 1;
    public bool HasNext => CurrentPage < TotalPages;
    public Dictionary<string, Uri> Links { get; set; } = [];
}

public class PaginationNextPageMetaddata
{
    public PaginationNextPageMetaddata(int pageSize, string? nextPageToken = null)
    {
        PageSize = pageSize;
        NextPageToken = nextPageToken;
    }

    public string? NextPageToken { get; set; }

    public int PageSize { get; set; }

    public Dictionary<string, Uri> Links { get; set; } = [];
}