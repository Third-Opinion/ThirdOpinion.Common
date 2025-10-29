namespace ThirdOpinion.Common.Aws.Misc;

/// <summary>
/// Service for signing HTTP requests with AWS Signature Version 4
/// </summary>
public interface IAwsSignatureService
{
    /// <summary>
    /// Signs an HTTP request message with AWS Signature V4
    /// </summary>
    /// <param name="request">The HTTP request to sign</param>
    /// <param name="service">The AWS service name (e.g., "healthlake")</param>
    /// <param name="region">The AWS region</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The signed HTTP request message</returns>
    Task<HttpRequestMessage> SignRequestAsync(
        HttpRequestMessage request, 
        string service, 
        string region, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Signs an HTTP request with body content
    /// </summary>
    /// <param name="request">The HTTP request to sign</param>
    /// <param name="requestBody">The request body content</param>
    /// <param name="service">The AWS service name</param>
    /// <param name="region">The AWS region</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The signed HTTP request message</returns>
    Task<HttpRequestMessage> SignRequestWithBodyAsync(
        HttpRequestMessage request,
        string requestBody,
        string service,
        string region,
        CancellationToken cancellationToken = default);
}