using System.Text;

namespace ThirdOpinion.Common.Aws.DynamoDb.Pagination;

public static class PaginationHelper
{
    public static Uri GetPageUri(PaginationQuery parameters, string baseUri, string route)
    {
        var builder = new UriBuilder(baseUri);
        builder.Path += route;

        var query = new StringBuilder();
        query.Append($"pageNumber={parameters.PageNumber}&pageSize={parameters.PageSize}");

        builder.Query = query.ToString();
        return builder.Uri;
    }

    public static PagedResponse<T> CreatePagedResponse<T>(
        List<T> data,
        PaginationQuery parameters,
        string baseUri,
        string route,
        int? totalRecords = null)
    {
        var metadata = new PaginationMetadata(
            parameters.PageNumber,
            parameters.PageSize,
            totalRecords);

        if (metadata.HasPrevious)
            metadata.Links.Add("previousPage", GetPageUri(
                new PaginationQuery(parameters.PageNumber - 1) { PageSize = parameters.PageSize },
                baseUri,
                route));

        if (metadata.HasNext)
            metadata.Links.Add("nextPage", GetPageUri(
                new PaginationQuery(parameters.PageNumber + 1) { PageSize = parameters.PageSize },
                baseUri,
                route));

        metadata.Links.Add("firstPage", GetPageUri(
            new PaginationQuery(1) { PageSize = parameters.PageSize },
            baseUri,
            route));

        if (metadata.TotalPages.HasValue)
            metadata.Links.Add("lastPage", GetPageUri(
                new PaginationQuery(metadata.TotalPages.Value) { PageSize = parameters.PageSize },
                baseUri,
                route));

        return new PagedResponse<T>
        {
            Items = data,
            Metadata = metadata
        };
    }
}