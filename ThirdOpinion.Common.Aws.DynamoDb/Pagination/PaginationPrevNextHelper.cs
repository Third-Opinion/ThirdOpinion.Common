namespace ThirdOpinion.Common.Aws.DynamoDb.Pagination;

/// <summary>
///     Provides helper methods for token-based pagination navigation
/// </summary>
public static class PaginationNextPageHelper
{
    /// <summary>
    ///     Builds the URI for the next page using a next page token
    /// </summary>
    /// <param name="nextPageToken">The token for the next page</param>
    /// <param name="pageSize">The number of items per page</param>
    /// <param name="baseUri">The base URI for the API</param>
    /// <param name="route">The route path for the endpoint</param>
    /// <returns>A URI pointing to the next page</returns>
    private static Uri GetNextPageUri(string nextPageToken,
        int pageSize,
        string baseUri,
        string route)
    {
        var queryParams = new List<string>
        {
            $"nextPageToken={nextPageToken}",
            $"pageSize={pageSize}"
        };

        return BuildUri(baseUri, route, queryParams);
    }

    /// <summary>
    ///     Builds a URI with the specified base URI, route, and query parameters
    /// </summary>
    /// <param name="baseUri">The base URI</param>
    /// <param name="route">The route path</param>
    /// <param name="queryParams">The query parameters to append</param>
    /// <returns>A complete URI with the specified components</returns>
    private static Uri BuildUri(string baseUri, string route, List<string> queryParams)
    {
        var builder = new UriBuilder(baseUri);

        builder.Path += route.TrimStart('/');

        if (queryParams.Any()) builder.Query = string.Join('&', queryParams);

        return builder.Uri;
    }
}