namespace ThirdOpinion.Common.Aws.DynamoDb.Pagination;

public static class PaginationNextPageHelper
{
    /// <summary>
    ///     Builds the URI for the next page
    /// </summary>
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

    private static Uri BuildUri(string baseUri, string route, List<string> queryParams)
    {
        var builder = new UriBuilder(baseUri);

        builder.Path += route.TrimStart('/');

        if (queryParams.Any()) builder.Query = string.Join('&', queryParams);

        return builder.Uri;
    }

}