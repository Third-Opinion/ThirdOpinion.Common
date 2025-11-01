using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ThirdOpinion.Common.AthenaEhr;

public class AthenaOAuthService : IAthenaOAuthService
{
    private readonly AthenaConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<AthenaOAuthService> _logger;
    private readonly ConcurrentDictionary<string, OAuthToken> _tokenCache = new();

    public AthenaOAuthService(
        HttpClient httpClient,
        ILogger<AthenaOAuthService> logger,
        IOptions<AthenaConfig> config)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
    }

    public async Task<OAuthToken> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{_config.ClientId}:{_config.PracticeId}";

        // Check cache if enabled
        if (_config.UseTokenCache && _tokenCache.TryGetValue(cacheKey, out OAuthToken? cachedToken))
            if (cachedToken != null && !cachedToken.IsExpired())
            {
                _logger.LogDebug("Returning cached OAuth token for practice {PracticeId}",
                    _config.PracticeId);
                return cachedToken;
            }

        _logger.LogInformation("Requesting new OAuth token for practice {PracticeId}",
            _config.PracticeId);

        var tokenEndpoint = $"{_config.BaseUrl}/oauth2/v1/token";

        var requestBody = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("client_id", _config.ClientId),
            new KeyValuePair<string, string>("client_secret", _config.ClientSecret)
        });

        var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = requestBody
        };

        try
        {
            HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync(cancellationToken);
                var tokenResponse = JsonSerializer.Deserialize<OAuthTokenResponse>(json,
                    new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                    });

                if (tokenResponse == null)
                    throw new OAuthException("Failed to deserialize OAuth token response");

                var token = new OAuthToken
                {
                    AccessToken = tokenResponse.AccessToken,
                    ExpiresIn = tokenResponse.ExpiresIn,
                    TokenType = tokenResponse.TokenType,
                    Scope = tokenResponse.Scope,
                    CreatedAt = DateTimeOffset.UtcNow
                };

                // Cache the token if caching is enabled
                if (_config.UseTokenCache)
                {
                    _tokenCache[cacheKey] = token;
                    _logger.LogDebug("Cached OAuth token for practice {PracticeId}",
                        _config.PracticeId);
                }

                _logger.LogInformation(
                    "Successfully obtained OAuth token for practice {PracticeId}",
                    _config.PracticeId);
                return token;
            }

            string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("OAuth token request failed with status {StatusCode}: {Content}",
                response.StatusCode, errorContent);
            throw new OAuthException(
                $"Failed to obtain OAuth token: {response.StatusCode} - {errorContent}");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error occurred while obtaining OAuth token");
            throw new OAuthException("HTTP error occurred while obtaining OAuth token", ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "OAuth token request timed out");
            throw new OAuthException("OAuth token request timed out", ex);
        }
    }

    public Task<OAuthToken> RefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        // Athena uses client_credentials grant type, which doesn't provide refresh tokens
        // Just get a new token instead
        _logger.LogInformation(
            "Athena OAuth uses client_credentials; requesting new token instead of refresh");
        return GetTokenAsync(cancellationToken);
    }

    public Task RevokeTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        // Clear from cache if present
        if (_config.UseTokenCache)
        {
            var cacheKey = $"{_config.ClientId}:{_config.PracticeId}";
            _tokenCache.TryRemove(cacheKey, out _);
            _logger.LogDebug("Cleared cached token for practice {PracticeId}", _config.PracticeId);
        }

        // Athena doesn't provide a token revocation endpoint for client_credentials
        _logger.LogInformation("Token revocation not supported for Athena client_credentials flow");
        return Task.CompletedTask;
    }
}