using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThirdOpinion.Common.Aws.HealthLake.Aws;
using ThirdOpinion.Common.Aws.HealthLake.Configuration;

namespace ThirdOpinion.Common.Aws.HealthLake.Http;

/// <summary>
///     Service for handling signed HTTP requests to AWS HealthLake
/// </summary>
public class HealthLakeHttpService : IHealthLakeHttpService
{
    private readonly IAwsSignatureService _awsSignatureService;
    private readonly HealthLakeConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<HealthLakeHttpService> _logger;

    /// <summary>
    ///     Initializes a new instance of the HealthLakeHttpService
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="httpClient">HTTP client for making requests</param>
    /// <param name="config">HealthLake configuration options</param>
    /// <param name="awsSignatureService">AWS signature service for signing requests</param>
    public HealthLakeHttpService(
        ILogger<HealthLakeHttpService> logger,
        HttpClient httpClient,
        IOptions<HealthLakeConfig> config,
        IAwsSignatureService awsSignatureService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _awsSignatureService = awsSignatureService ??
                                throw new ArgumentNullException(nameof(awsSignatureService));
    }

    /// <inheritdoc />
    public async Task<HttpResponseMessage> SendSignedRequestAsync(HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        _logger.LogDebug("Signing and sending HTTP request to {Uri}", request.RequestUri);

        try
        {
            // Sign the request with AWS Signature V4 (modifies request in place)
            await _awsSignatureService.SignRequestAsync(request, "healthlake", _config.Region);

            HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);

            _logger.LogDebug("Received response with status {StatusCode}", response.StatusCode);

            return response;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed for {Uri}", request.RequestUri);
            throw;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "HTTP request timed out for {Uri}", request.RequestUri);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<HttpResponseMessage> SendSignedRequestAsync(HttpRequestMessage request,
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        _logger.LogDebug("Signing and sending HTTP request to {Uri} with service {ServiceName}",
            request.RequestUri, serviceName);

        try
        {
            // Sign the request with AWS Signature V4 using the specified service name
            await _awsSignatureService.SignRequestAsync(request, serviceName, _config.Region);

            HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);

            _logger.LogDebug("Received response with status {StatusCode}", response.StatusCode);

            return response;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed for {Uri}", request.RequestUri);
            throw;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "HTTP request timed out for {Uri}", request.RequestUri);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<HttpRequestMessage> CloneHttpRequestAsync(HttpRequestMessage request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version
        };

        // Copy headers
        foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        // Copy content if present
        if (request.Content != null)
        {
            byte[] contentBytes = await request.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(contentBytes);

            // Copy content headers
            foreach (KeyValuePair<string, IEnumerable<string>> header in request.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}