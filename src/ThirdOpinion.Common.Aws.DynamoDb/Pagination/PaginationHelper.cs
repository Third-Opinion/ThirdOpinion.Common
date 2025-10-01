using System.Text;

namespace ThirdOpinion.Common.Aws.DynamoDb.Pagination;

/// <summary>
///     Provides helper methods for creating paginated responses and navigation URIs
/// </summary>
public static class PaginationHelper
{
    /// <summary>
    ///     Creates a URI for a specific page using the provided parameters
    /// </summary>
    /// <param name="parameters">The pagination parameters</param>
    /// <param name="baseUri">The base URI for the API</param>
    /// <param name="route">The route path for the endpoint</param>
    /// <returns>A URI pointing to the specified page</returns>
    public static Uri GetPageUri(PaginationQuery parameters, string baseUri, string route)
    {
        var builder = new UriBuilder(baseUri);
        builder.Path = builder.Path.TrimEnd('/') + "/" + route.TrimStart('/');

        var query = new StringBuilder();
        query.Append($"pageNumber={parameters.PageNumber}&pageSize={parameters.PageSize}");

        builder.Query = query.ToString();
        return builder.Uri;
    }

    /// <summary>
    ///     Creates a paginated response with navigation links and metadata
    /// </summary>
    /// <typeparam name="T">The type of items in the paginated response</typeparam>
    /// <param name="data">The data items for the current page</param>
    /// <param name="parameters">The pagination parameters</param>
    /// <param name="baseUri">The base URI for the API</param>
    /// <param name="route">The route path for the endpoint</param>
    /// <param name="totalRecords">The total number of records (optional)</param>
    /// <returns>A paginated response with navigation links and metadata</returns>
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