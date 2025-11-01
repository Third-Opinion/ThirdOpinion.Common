using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ThirdOpinion.Common.AthenaEhr;

/// <summary>
///     Service for retrieving FHIR resources from Athena Health API
/// </summary>
public class AthenaFhirService : IFhirSourceService
{
    // Semaphore to control parallel operations (max 10 concurrent)
    private readonly SemaphoreSlim _concurrencySemaphore = new(10, 10);
    private readonly AthenaConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<AthenaFhirService> _logger;
    private readonly IAthenaOAuthService _oauthService;

    public AthenaFhirService(
        HttpClient httpClient,
        IAthenaOAuthService oauthService,
        IOptions<AthenaConfig> config,
        ILogger<AthenaFhirService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _oauthService = oauthService ?? throw new ArgumentNullException(nameof(oauthService));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<string?> GetResourceAsync(string resourceType,
        string resourceId,
        CancellationToken cancellationToken = default)
    {
        ValidateResourceType(resourceType);
        ValidateResourceId(resourceId);

        _logger.LogInformation("Starting Athena GET operation for {ResourceType}/{ResourceId}",
            resourceType, resourceId);

        await _concurrencySemaphore.WaitAsync(cancellationToken);
        try
        {
            // Get OAuth token
            OAuthToken token = await _oauthService.GetTokenAsync(cancellationToken);

            // Prepare the request
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"/v1/{_config.PracticeId}/fhir/r4/{resourceType}/{resourceId}");
            request.Headers.Authorization
                = new AuthenticationHeaderValue("Bearer", token.AccessToken);

            // Send the request
            HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                string content = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogInformation(
                    "Successfully retrieved FHIR resource from Athena: {ResourceType}/{ResourceId}",
                    resourceType, resourceId);
                return content;
            }

            await HandleErrorResponseAsync(response, resourceType, resourceId, cancellationToken);
            return string.Empty; // Will not reach here due to exception
        }
        catch (FhirResourceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error retrieving FHIR resource from Athena: {ResourceType}/{ResourceId}",
                resourceType, resourceId);
            throw new FhirResourceException(
                $"Failed to retrieve FHIR resource from Athena: {ex.Message}",
                resourceType,
                resourceId,
                ex);
        }
        finally
        {
            _concurrencySemaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task<T?> GetResourceAsync<T>(string resourceType,
        string resourceId,
        CancellationToken cancellationToken = default) where T : class
    {
        string? json = await GetResourceAsync(resourceType, resourceId, cancellationToken);
        if (string.IsNullOrEmpty(json))
            return null;

        return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, string?>> GetResourcesAsync(
        IEnumerable<(string ResourceType, string ResourceId)> resources,
        CancellationToken cancellationToken = default)
    {
        if (resources == null)
            throw new ArgumentNullException(nameof(resources));

        List<(string ResourceType, string ResourceId)> resourceList = resources.ToList();
        if (!resourceList.Any())
            return new Dictionary<string, string?>();

        _logger.LogInformation("Starting batch Athena GET operation for {Count} resources",
            resourceList.Count);

        var results = new Dictionary<string, string?>();
        IEnumerable<Task> tasks = resourceList.Select(async resource =>
        {
            var key = $"{resource.ResourceType}/{resource.ResourceId}";
            try
            {
                string? content = await GetResourceAsync(resource.ResourceType, resource.ResourceId,
                    cancellationToken);
                lock (results)
                {
                    results[key] = content;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve resource in batch operation: {Key}", key);
                lock (results)
                {
                    results[key] = null;
                }
            }
        });

        await Task.WhenAll(tasks);

        _logger.LogInformation(
            "Completed batch Athena GET operation: {SuccessCount}/{TotalCount} successful",
            results.Count(r => !string.IsNullOrEmpty(r.Value)), resourceList.Count);

        return results;
    }

    /// <inheritdoc />
    public bool IsResourceTypeSupported(string resourceType)
    {
        // Athena supports a limited set of FHIR resources
        var supportedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Patient",
            "Practitioner",
            "Organization",
            "AllergyIntolerance",
            "Condition",
            "Immunization",
            "MedicationRequest",
            "MedicationStatement",
            "Observation",
            "Procedure",
            "DocumentReference"
        };

        return !string.IsNullOrWhiteSpace(resourceType) && supportedTypes.Contains(resourceType);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> GetSupportedResourceTypes()
    {
        return new List<string>
        {
            "Patient",
            "Practitioner",
            "Organization",
            "AllergyIntolerance",
            "Condition",
            "Immunization",
            "MedicationRequest",
            "MedicationStatement",
            "Observation",
            "Procedure",
            "DocumentReference"
        };
    }

    /// <inheritdoc />
    public async Task<string> SearchResourcesAsync(string resourceType,
        string searchParams,
        CancellationToken cancellationToken = default)
    {
        ValidateResourceType(resourceType);

        _logger.LogInformation(
            "Starting Athena search operation for {ResourceType} with params: {SearchParams}",
            resourceType, searchParams);

        await _concurrencySemaphore.WaitAsync(cancellationToken);
        try
        {
            // Get OAuth token
            OAuthToken token = await _oauthService.GetTokenAsync(cancellationToken);

            // Prepare the request
            var url = $"/v1/{_config.PracticeId}/fhir/r4/{resourceType}";
            if (!string.IsNullOrWhiteSpace(searchParams)) url += $"?{searchParams}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization
                = new AuthenticationHeaderValue("Bearer", token.AccessToken);

            // Send the request
            HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                string content = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogInformation(
                    "Successfully searched FHIR resources from Athena: {ResourceType}",
                    resourceType);
                return content;
            }

            await HandleErrorResponseAsync(response, resourceType, null, cancellationToken);
            return string.Empty; // Will not reach here due to exception
        }
        catch (FhirResourceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching FHIR resources from Athena: {ResourceType}",
                resourceType);
            throw new FhirResourceException(
                $"Failed to search FHIR resources from Athena: {ex.Message}",
                resourceType,
                null,
                ex);
        }
        finally
        {
            _concurrencySemaphore.Release();
        }
    }

    private async Task HandleErrorResponseAsync(HttpResponseMessage response,
        string resourceType,
        string? resourceId,
        CancellationToken cancellationToken)
    {
        string content = await response.Content.ReadAsStringAsync(cancellationToken);

        switch (response.StatusCode)
        {
            case HttpStatusCode.NotFound:
                throw new FhirResourceNotFoundException(resourceType, resourceId ?? "unknown");

            case HttpStatusCode.Unauthorized:
                throw new FhirAuthenticationException(
                    "Authentication failed for Athena API access");

            case HttpStatusCode.TooManyRequests:
                throw new FhirRateLimitException(
                    "Athena API rate limit exceeded",
                    DateTimeOffset.UtcNow.AddSeconds(60)); // Default retry after 1 minute

            default:
                throw new FhirResourceException(
                    $"Athena API error: {response.StatusCode} - {content}",
                    resourceType,
                    resourceId,
                    response.StatusCode);
        }
    }

    private static void ValidateResourceType(string resourceType)
    {
        if (string.IsNullOrWhiteSpace(resourceType))
            throw new ArgumentException("Resource type cannot be null or empty.",
                nameof(resourceType));
    }

    private static void ValidateResourceId(string resourceId)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
            throw new ArgumentException("Resource ID cannot be null or empty.", nameof(resourceId));
    }
}