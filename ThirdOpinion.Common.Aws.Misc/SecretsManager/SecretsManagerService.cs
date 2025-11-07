using System.Net;
using System.Text.Json;
using Amazon;
using Amazon.Runtime;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using ThirdOpinion.Common.Aws.Misc.Configuration;

namespace ThirdOpinion.Common.Aws.Misc.SecretsManager;

/// <summary>
///     Service for retrieving secrets from AWS Secrets Manager with caching support
/// </summary>
public class SecretsManagerService : ISecretsManagerService
{
    private readonly IMemoryCache _cache;
    private readonly SecretsManagerConfig _config;
    private readonly ILogger<SecretsManagerService> _logger;
    private readonly IAmazonSecretsManager _secretsManager;

    public SecretsManagerService(
        IOptions<SecretsManagerConfig> config,
        IMemoryCache cache,
        ILogger<SecretsManagerService> logger,
        IAmazonSecretsManager? secretsManager = null)
    {
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Use injected client or create AWS Secrets Manager client with the configured region
        _secretsManager = secretsManager ?? CreateSecretsManagerClient(_config.Region);
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, string>> GetSecretAsync(CancellationToken cancellationToken
        = default)
    {
        return await GetSecretAsync(_config.SecretName, _config.Region, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, string>> GetSecretAsync(
        string secretName,
        string region,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(secretName))
            throw new ArgumentException("Secret name cannot be null or empty.", nameof(secretName));

        if (string.IsNullOrWhiteSpace(region))
            throw new ArgumentException("Region cannot be null or empty.", nameof(region));

        var cacheKey = $"secret:{secretName}:{region}";

        // Try to get from cache first if caching is enabled
        if (_config.EnableCaching &&
            _cache.TryGetValue(cacheKey, out Dictionary<string, string>? cachedSecret))
        {
            _logger.LogDebug("Secret retrieved from cache: {SecretName}", secretName);
            return cachedSecret!;
        }

        _logger.LogDebug(
            "Retrieving secret from AWS Secrets Manager: {SecretName} in region {Region}",
            secretName, region);

        try
        {
            // Use the instance client if the region matches, otherwise create a temporary one
            IAmazonSecretsManager client;
            AmazonSecretsManagerClient? tempClient = null;

            if (string.Equals(region, _config.Region, StringComparison.OrdinalIgnoreCase))
            {
                client = _secretsManager;
            }
            else
            {
                tempClient = new AmazonSecretsManagerClient(RegionEndpoint.GetBySystemName(region));
                client = tempClient;
            }

            try
            {
                var request = new GetSecretValueRequest
                {
                    SecretId = secretName,
                    VersionStage = "AWSCURRENT"
                };

                IAsyncPolicy retryPolicy = CreateRetryPolicy();
                GetSecretValueResponse? response = await retryPolicy.ExecuteAsync(async () =>
                    await client.GetSecretValueAsync(request, cancellationToken));

                if (string.IsNullOrWhiteSpace(response.SecretString))
                    throw new InvalidOperationException(
                        $"Secret '{secretName}' returned empty or null value");

                // Parse the JSON secret
                Dictionary<string, string> secretDictionary
                    = ParseSecretJson(response.SecretString, secretName);

                // Cache the secret if caching is enabled
                if (_config.EnableCaching)
                {
                    var cacheOptions = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow
                            = TimeSpan.FromMinutes(_config.CacheTtlMinutes),
                        Priority = CacheItemPriority.High
                    };

                    _cache.Set(cacheKey, secretDictionary, cacheOptions);
                    _logger.LogDebug("Secret cached with TTL of {TTL} minutes: {SecretName}",
                        _config.CacheTtlMinutes, secretName);
                }

                _logger.LogInformation("Successfully retrieved secret: {SecretName}", secretName);
                return secretDictionary;
            }
            finally
            {
                // Dispose the temporary client if we created one
                tempClient?.Dispose();
            }
        }
        catch (ResourceNotFoundException ex)
        {
            _logger.LogError(ex, "Secret not found: {SecretName} in region {Region}", secretName,
                region);
            throw new InvalidOperationException(
                $"Secret '{secretName}' not found in region '{region}'", ex);
        }
        catch (AmazonSecretsManagerException ex) when (ex.ErrorCode == "AccessDeniedException")
        {
            _logger.LogError(ex, "Access denied to secret: {SecretName} in region {Region}",
                secretName, region);
            throw new UnauthorizedAccessException(
                $"Access denied to secret '{secretName}' in region '{region}'", ex);
        }
        catch (AmazonSecretsManagerException ex) when (ex.ErrorCode == "InvalidParameterException")
        {
            _logger.LogError(ex, "Invalid parameter for secret request: {SecretName}", secretName);
            throw new ArgumentException(
                $"Invalid parameter for secret '{secretName}': {ex.Message}", ex);
        }
        catch (AmazonSecretsManagerException ex) when (ex.ErrorCode == "InvalidRequestException")
        {
            _logger.LogError(ex, "Invalid request for secret: {SecretName}", secretName);
            throw new InvalidOperationException(
                $"Invalid request for secret '{secretName}': {ex.Message}", ex);
        }
        catch (AmazonSecretsManagerException ex) when (ex.ErrorCode == "DecryptionFailureException")
        {
            _logger.LogError(ex, "Failed to decrypt secret: {SecretName}", secretName);
            throw new InvalidOperationException($"Failed to decrypt secret '{secretName}'", ex);
        }
        catch (AmazonSecretsManagerException ex) when (ex.ErrorCode ==
                                                       "InternalServiceErrorException")
        {
            _logger.LogError(ex, "AWS Secrets Manager internal error for secret: {SecretName}",
                secretName);
            throw new InvalidOperationException(
                $"AWS Secrets Manager internal error for secret '{secretName}'", ex);
        }
        catch (AmazonSecretsManagerException ex)
        {
            _logger.LogError(ex,
                "AWS Secrets Manager error for secret: {SecretName}, ErrorCode: {ErrorCode}",
                secretName, ex.ErrorCode);
            throw new InvalidOperationException(
                $"AWS Secrets Manager error for secret '{secretName}': {ex.Message}", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse secret JSON: {SecretName}", secretName);
            throw new InvalidOperationException(
                $"Failed to parse secret '{secretName}' as valid JSON", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving secret: {SecretName}", secretName);
            throw new InvalidOperationException(
                $"Unexpected error retrieving secret '{secretName}': {ex.Message}", ex);
        }
    }

    public void Dispose()
    {
        _secretsManager?.Dispose();
    }

    private static IAmazonSecretsManager CreateSecretsManagerClient(string region)
    {
        if (string.IsNullOrWhiteSpace(region)) return new AmazonSecretsManagerClient();

        try
        {
            RegionEndpoint? regionEndpoint = RegionEndpoint.GetBySystemName(region);
            return new AmazonSecretsManagerClient(regionEndpoint);
        }
        catch (ArgumentException)
        {
            // If region is invalid, use default region
            return new AmazonSecretsManagerClient();
        }
    }

    private static Dictionary<string, string> ParseSecretJson(string secretJson, string secretName)
    {
        try
        {
            using JsonDocument jsonDocument = JsonDocument.Parse(secretJson);
            var dictionary = new Dictionary<string, string>();

            foreach (JsonProperty property in jsonDocument.RootElement.EnumerateObject())
            {
                // Convert all values to strings
                string value = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                    JsonValueKind.Number => property.Value.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Null => string.Empty,
                    _ => property.Value.GetRawText()
                };

                dictionary[property.Name] = value;
            }

            return dictionary;
        }
        catch (JsonException ex)
        {
            throw new JsonException(
                $"Failed to parse secret '{secretName}' as valid JSON: {ex.Message}", ex);
        }
    }

    private IAsyncPolicy CreateRetryPolicy()
    {
        return Policy
            .Handle<AmazonServiceException>(ex => IsTransientError(ex))
            .Or<TimeoutException>()
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(
                3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) +
                                TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000)),
                (exception, timespan, retryCount, context) =>
                {
                    _logger.LogWarning(exception,
                        "Retry attempt {RetryCount} for AWS Secrets Manager operation. Waiting {Delay}ms before next retry. Exception: {ExceptionType}",
                        retryCount, timespan.TotalMilliseconds, exception?.GetType().Name);
                });
    }

    private static bool IsTransientError(Exception exception)
    {
        if (exception is AmazonServiceException awsException)
            // Handle AWS transient errors
            return awsException.ErrorCode switch
            {
                "ThrottlingException" => true,
                "InternalServiceErrorException" => true,
                "ServiceUnavailableException" => true,
                "RequestTimeoutException" => true,
                _ when awsException.StatusCode == HttpStatusCode.InternalServerError => true,
                _ when awsException.StatusCode == HttpStatusCode.BadGateway => true,
                _ when awsException.StatusCode == HttpStatusCode.ServiceUnavailable => true,
                _ when awsException.StatusCode == HttpStatusCode.GatewayTimeout => true,
                _ when awsException.StatusCode == HttpStatusCode.TooManyRequests => true,
                _ => false
            };

        // Handle other transient errors
        return exception is TimeoutException or TaskCanceledException;
    }
}