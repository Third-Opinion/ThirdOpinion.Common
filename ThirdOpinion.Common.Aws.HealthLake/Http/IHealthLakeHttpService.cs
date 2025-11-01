namespace ThirdOpinion.Common.Aws.HealthLake.Http;

/// <summary>
///     Service for handling signed HTTP requests to AWS HealthLake
/// </summary>
public interface IHealthLakeHttpService
{
    /// <summary>
    ///     Sends a signed HTTP request to HealthLake with comprehensive error handling
    /// </summary>
    /// <param name="request">The HTTP request to send</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The HTTP response</returns>
    Task<HttpResponseMessage> SendSignedRequestAsync(HttpRequestMessage request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Sends a signed HTTP request to HealthLake with custom AWS service name
    /// </summary>
    /// <param name="request">The HTTP request to send</param>
    /// <param name="serviceName">The AWS service name for signing (defaults to "healthlake")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The HTTP response</returns>
    Task<HttpResponseMessage> SendSignedRequestAsync(HttpRequestMessage request,
        string serviceName,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Creates a deep clone of an HTTP request message for retry scenarios
    /// </summary>
    /// <param name="original">The original HTTP request message to clone</param>
    /// <returns>A cloned HTTP request message</returns>
    Task<HttpRequestMessage> CloneHttpRequestAsync(HttpRequestMessage original);
}