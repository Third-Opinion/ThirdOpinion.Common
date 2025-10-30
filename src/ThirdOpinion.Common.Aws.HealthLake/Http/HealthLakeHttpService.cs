using System.Net.Http;
using ThirdOpinion.Common.Aws.HealthLake.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ThirdOpinion.Common.Aws.HealthLake.Http;

/// <summary>
/// Service for handling signed HTTP requests to AWS HealthLake
/// </summary>
public class HealthLakeHttpService : IHealthLakeHttpService
{
    private readonly ILogger<HealthLakeHttpService> _logger;
    private readonly HttpClient _httpClient;
    private readonly HealthLakeConfig _config;

    /// <summary>
    /// Initializes a new instance of the HealthLakeHttpService
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="httpClient">HTTP client for making requests</param>
    /// <param name="config">HealthLake configuration options</param>
    public HealthLakeHttpService(
        ILogger<HealthLakeHttpService> logger,
        HttpClient httpClient,
        IOptions<HealthLakeConfig> config)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
    }

    /// <inheritdoc />
    public async Task<HttpResponseMessage> SendSignedRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        _logger.LogDebug("Sending signed HTTP request to {Uri}", request.RequestUri);

        try
        {
            // TODO: Add AWS signature V4 signing here
            // For now, sending without signing - this will need to be implemented
            // with proper AWS credentials and signing logic

            var response = await _httpClient.SendAsync(request, cancellationToken);

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
    public async Task<HttpResponseMessage> SendSignedRequestAsync(HttpRequestMessage request, string serviceName, CancellationToken cancellationToken = default)
    {
        // For now, just delegate to the main method
        return await SendSignedRequestAsync(request, cancellationToken);
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
        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        // Copy content if present
        if (request.Content != null)
        {
            var contentBytes = await request.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(contentBytes);

            // Copy content headers
            foreach (var header in request.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
    }
}