using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Amazon.HealthLake;
using Amazon.HealthLake.Model;
using ThirdOpinion.Common.Aws.HealthLake.Configuration;
using ThirdOpinion.Common.Aws.HealthLake.Exceptions;
using ThirdOpinion.Common.Aws.HealthLake.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ThirdOpinion.Common.Aws.HealthLake;

/// <summary>
/// Service for writing FHIR resources to AWS HealthLake
/// </summary>
public class HealthLakeFhirService : IFhirDestinationService
{
    private readonly IAmazonHealthLake _healthLakeClient;
    private readonly HealthLakeConfig _config;
    private readonly ILogger<HealthLakeFhirService> _logger;
    private readonly IHealthLakeHttpService _healthLakeHttpService;

    // Semaphore to control parallel operations (max 10 concurrent writes)
    private readonly SemaphoreSlim _concurrencySemaphore = new(10, 10);

    // Supported FHIR resource types for HealthLake
    private static readonly HashSet<string> SupportedResourceTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Patient",
        "Practitioner",
        "PractitionerRole",
        "Organization",
        "Location",
        "HealthcareService",
        "Endpoint",
        "Medication",
        "MedicationDispense",
        "MedicationRequest",
        "MedicationStatement",
        "MedicationAdministration",
        "Observation",
        "DiagnosticReport",
        "Condition",
        "Procedure",
        "Immunization",
        "AllergyIntolerance",
        "CarePlan",
        "CareTeam",
        "Goal",
        "ServiceRequest",
        "DeviceRequest",
        "Coverage",
        "Encounter",
        "EpisodeOfCare",
        "Device",
        "RelatedPerson",
        "Specimen",
        "DocumentReference",
        "Provenance",
        "AuditEvent",
        "Consent",
        "ResearchStudy",
        "ResearchSubject",
        "Task",
        "Communication",
        "CommunicationRequest",
        "Media",
        "Binary",
        "Bundle",
        "Composition",
        "List",
        "Library",
        "Measure",
        "MeasureReport"
    };

    public HealthLakeFhirService(
        IAmazonHealthLake healthLakeClient,
        IOptions<HealthLakeConfig> config,
        ILogger<HealthLakeFhirService> logger,
        IHealthLakeHttpService healthLakeHttpService)
    {
        _healthLakeClient = healthLakeClient ?? throw new ArgumentNullException(nameof(healthLakeClient));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _healthLakeHttpService = healthLakeHttpService ?? throw new ArgumentNullException(nameof(healthLakeHttpService));
    }

    /// <inheritdoc />
    public async Task PutResourceAsync(string resourceType, string resourceId, string resourceJson, CancellationToken cancellationToken = default)
    {
        await PutResourceAsync(resourceType, resourceId, resourceJson, null, cancellationToken);
    }

    /// <summary>
    /// Writes a FHIR resource to HealthLake with optional version control
    /// </summary>
    public async Task<string?> PutResourceAsync(string resourceType, string resourceId, string resourceJson, string? ifMatchVersion, CancellationToken cancellationToken = default)
    {
        ValidateResourceType(resourceType);
        ValidateResourceId(resourceId);
        ValidateResourceJson(resourceJson);

        _logger.LogInformation("Starting HealthLake PUT operation for {ResourceType}/{ResourceId}",
            resourceType, resourceId);

        await _concurrencySemaphore.WaitAsync(cancellationToken);
        try
        {
            // Use direct HTTP PUT to HealthLake FHIR endpoint for better control
            var endpoint = $"https://healthlake.{_config.Region}.amazonaws.com/datastore/{_config.DatastoreId}/r4/{resourceType}/{resourceId}";

            using var request = new HttpRequestMessage(HttpMethod.Put, endpoint);

            // Create content and set exact Content-Type header without charset
            request.Content = new StringContent(resourceJson, Encoding.UTF8);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/fhir+json");

            // Add If-Match header for version control if provided
            if (!string.IsNullOrWhiteSpace(ifMatchVersion))
            {
                request.Headers.Add("If-Match", ifMatchVersion);
                _logger.LogDebug("Added If-Match header for version control: {Version}", ifMatchVersion);
            }

            // Add validation level header (strict validation)
            request.Headers.Add("x-amz-fhir-validation-level", "strict");

            // Send the request with AWS credentials
            var response = await _healthLakeHttpService.SendSignedRequestAsync(request, cancellationToken);

            // Handle response and extract version
            var version = await HandlePutResponseAsync(response, resourceType, resourceId, cancellationToken);

            _logger.LogInformation("Successfully wrote FHIR resource to HealthLake: {ResourceType}/{ResourceId} Version: {Version}",
                resourceType, resourceId, version ?? "unknown");

            return version;
        }
        catch (HealthLakeException)
        {
            throw; // Re-throw HealthLake-specific exceptions
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing FHIR resource to HealthLake: {ResourceType}/{ResourceId}",
                resourceType, resourceId);
            throw new HealthLakeException(
                $"Failed to write FHIR resource to HealthLake: {ex.Message}",
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
    public async Task PutResourceAsync<T>(string resourceType, string resourceId, T resource, CancellationToken cancellationToken = default) where T : class
    {
        if (resource == null)
            throw new ArgumentNullException(nameof(resource));

        var json = JsonSerializer.Serialize(resource, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        await PutResourceAsync(resourceType, resourceId, json, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, bool>> PutResourcesAsync(
        IEnumerable<(string ResourceType, string ResourceId, string ResourceJson)> resources,
        CancellationToken cancellationToken = default)
    {
        if (resources == null)
            throw new ArgumentNullException(nameof(resources));

        var resourceList = resources.ToList();
        if (!resourceList.Any())
            return new Dictionary<string, bool>();

        _logger.LogInformation("Starting batch HealthLake PUT operation for {Count} resources", resourceList.Count);

        var results = new Dictionary<string, bool>();
        var tasks = resourceList.Select(async resource =>
        {
            var key = $"{resource.ResourceType}/{resource.ResourceId}";
            try
            {
                await PutResourceAsync(resource.ResourceType, resource.ResourceId, resource.ResourceJson, cancellationToken);
                lock (results)
                {
                    results[key] = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write resource in batch operation: {Key}", key);
                lock (results)
                {
                    results[key] = false;
                }
            }
        });

        await Task.WhenAll(tasks);

        var successCount = results.Values.Count(success => success);
        _logger.LogInformation("Completed batch HealthLake PUT operation: {SuccessCount}/{TotalCount} successful",
            successCount, resourceList.Count);

        return results;
    }

    /// <inheritdoc />
    public bool IsResourceTypeSupported(string resourceType)
    {
        return !string.IsNullOrWhiteSpace(resourceType) &&
               SupportedResourceTypes.Contains(resourceType);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetSupportedResourceTypes()
    {
        return SupportedResourceTypes.ToList().AsReadOnly();
    }

    public async Task<WriteResult> WriteResourceAsync(string resourceJson, CancellationToken cancellationToken = default)
    {
        try
        {
            // Parse resource to get type and ID
            using var doc = JsonDocument.Parse(resourceJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("resourceType", out var resourceTypeElement))
            {
                return new WriteResult
                {
                    Success = false,
                    ErrorMessage = "Resource JSON does not contain resourceType",
                    IsRetryable = false
                };
            }

            var resourceType = resourceTypeElement.GetString() ?? string.Empty;

            if (!root.TryGetProperty("id", out var idElement))
            {
                return new WriteResult
                {
                    Success = false,
                    ErrorMessage = "Resource JSON does not contain id",
                    IsRetryable = false
                };
            }

            var resourceId = idElement.GetString() ?? string.Empty;

            // Use existing PutResourceAsync method
            await PutResourceAsync(resourceType, resourceId, resourceJson, cancellationToken);

            return new WriteResult
            {
                Success = true
            };
        }
        catch (HealthLakeException ex)
        {
            return new WriteResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                IsRetryable = ex.StatusCode.HasValue && IsRetryableStatusCode(ex.StatusCode.Value)
            };
        }
        catch (Exception ex)
        {
            return new WriteResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                IsRetryable = ex is HttpRequestException || ex is TaskCanceledException
            };
        }
    }

    private async Task<string?> HandlePutResponseAsync(HttpResponseMessage response, string resourceType, string resourceId, CancellationToken cancellationToken)
    {
        string? version = null;

        // Try to extract version from ETag header
        if (response.Headers.ETag != null)
        {
            version = response.Headers.ETag.Tag?.Trim('"', 'W', '/');
            _logger.LogDebug("Extracted version from ETag: {Version}", version);
        }

        switch (response.StatusCode)
        {
            case HttpStatusCode.OK:
            case HttpStatusCode.Created:
                // Success - return the version if found
                return version;

            case HttpStatusCode.BadRequest:
                var badRequestContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var badRequestDetails = await ExtractOperationOutcomeAsync(badRequestContent);
                throw new HealthLakeException(
                    $"Invalid FHIR resource format: {badRequestDetails ?? badRequestContent}",
                    resourceType,
                    resourceId,
                    HttpStatusCode.BadRequest);

            case HttpStatusCode.Unauthorized:
                throw new HealthLakeException(
                    "Authentication failed for HealthLake access",
                    resourceType,
                    resourceId,
                    HttpStatusCode.Unauthorized);

            case HttpStatusCode.Forbidden:
                throw new HealthLakeAccessDeniedException(
                    $"Access denied to HealthLake datastore for resource {resourceType}/{resourceId}");

            case HttpStatusCode.Conflict:
                var conflictContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var conflictDetails = await ExtractOperationOutcomeAsync(conflictContent);
                throw new HealthLakeConflictException(
                    resourceType,
                    resourceId,
                    conflictDetails ?? "Version conflict detected");

            case HttpStatusCode.TooManyRequests:
                var retryAfter = response.Headers.RetryAfter?.Delta;
                throw new HealthLakeThrottlingException(
                    "HealthLake API rate limit exceeded",
                    retryAfter.HasValue ? DateTimeOffset.UtcNow.Add(retryAfter.Value) : null);

            case HttpStatusCode.InternalServerError:
            case HttpStatusCode.BadGateway:
            case HttpStatusCode.ServiceUnavailable:
            case HttpStatusCode.GatewayTimeout:
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new HealthLakeException(
                    $"HealthLake service error: {response.StatusCode} - {errorContent}",
                    resourceType,
                    resourceId,
                    response.StatusCode);

            default:
                var unknownErrorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new HealthLakeException(
                    $"Unexpected HealthLake response: {response.StatusCode} - {unknownErrorContent}",
                    resourceType,
                    resourceId,
                    response.StatusCode);
        }
    }

    private Task<string?> ExtractOperationOutcomeAsync(string responseContent)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(responseContent))
                return Task.FromResult<string?>(null);

            using var doc = JsonDocument.Parse(responseContent);
            var root = doc.RootElement;

            // Check if this is an OperationOutcome resource
            if (root.TryGetProperty("resourceType", out var resourceType) &&
                resourceType.GetString() == "OperationOutcome")
            {
                // Extract issue details
                if (root.TryGetProperty("issue", out var issues) && issues.ValueKind == JsonValueKind.Array)
                {
                    var issueMessages = new List<string>();
                    foreach (var issue in issues.EnumerateArray())
                    {
                        if (issue.TryGetProperty("diagnostics", out var diagnostics))
                        {
                            issueMessages.Add(diagnostics.GetString() ?? string.Empty);
                        }
                        else if (issue.TryGetProperty("details", out var details) &&
                                 details.TryGetProperty("text", out var text))
                        {
                            issueMessages.Add(text.GetString() ?? string.Empty);
                        }
                    }

                    if (issueMessages.Any())
                    {
                        return Task.FromResult<string?>(string.Join("; ", issueMessages));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse OperationOutcome from response");
        }

        return Task.FromResult<string?>(null);
    }

    private static void ValidateResourceType(string resourceType)
    {
        if (string.IsNullOrWhiteSpace(resourceType))
            throw new ArgumentException("Resource type cannot be null or empty.", nameof(resourceType));

        if (!SupportedResourceTypes.Contains(resourceType))
            throw new ArgumentException(
                $"Resource type '{resourceType}' is not supported. Supported types: {string.Join(", ", SupportedResourceTypes)}",
                nameof(resourceType));
    }

    private static bool IsRetryableStatusCode(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.TooManyRequests ||
               statusCode == HttpStatusCode.ServiceUnavailable ||
               statusCode == HttpStatusCode.GatewayTimeout ||
               statusCode == HttpStatusCode.RequestTimeout;
    }

    private static void ValidateResourceId(string resourceId)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
            throw new ArgumentException("Resource ID cannot be null or empty.", nameof(resourceId));
    }

    private static void ValidateResourceJson(string resourceJson)
    {
        if (string.IsNullOrWhiteSpace(resourceJson))
            throw new ArgumentException("Resource JSON cannot be null or empty.", nameof(resourceJson));

        // Basic JSON validation
        try
        {
            JsonDocument.Parse(resourceJson);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Invalid JSON format: {ex.Message}", nameof(resourceJson), ex);
        }
    }

    public void Dispose()
    {
        _concurrencySemaphore?.Dispose();
        _healthLakeClient?.Dispose();
    }
}