using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Amazon.HealthLake;
using Hl7.Fhir.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThirdOpinion.Common.Aws.HealthLake.Configuration;
using ThirdOpinion.Common.Aws.HealthLake.Exceptions;
using ThirdOpinion.Common.Aws.HealthLake.Http;

namespace ThirdOpinion.Common.Aws.HealthLake;

/// <summary>
///     Service for writing and reading FHIR resources to/from AWS HealthLake
/// </summary>
public class HealthLakeFhirService : IFhirDestinationService, IFhirSourceService
{
    // Supported FHIR resource types for HealthLake
    private static readonly HashSet<string> SupportedResourceTypes
        = new(StringComparer.OrdinalIgnoreCase)
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

    // Semaphore to control parallel operations (max 10 concurrent writes)
    private readonly SemaphoreSlim _concurrencySemaphore = new(10, 10);
    private readonly HealthLakeConfig _config;
    private readonly IAmazonHealthLake _healthLakeClient;
    private readonly IHealthLakeHttpService _healthLakeHttpService;
    private readonly ILogger<HealthLakeFhirService> _logger;

    /// <summary>
    ///     Initializes a new instance of the HealthLakeFhirService
    /// </summary>
    /// <param name="healthLakeClient">The AWS HealthLake client</param>
    /// <param name="config">HealthLake configuration options</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="healthLakeHttpService">HTTP service for HealthLake requests</param>
    public HealthLakeFhirService(
        IAmazonHealthLake healthLakeClient,
        IOptions<HealthLakeConfig> config,
        ILogger<HealthLakeFhirService> logger,
        IHealthLakeHttpService healthLakeHttpService)
    {
        _healthLakeClient = healthLakeClient ??
                            throw new ArgumentNullException(nameof(healthLakeClient));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _healthLakeHttpService = healthLakeHttpService ??
                                 throw new ArgumentNullException(nameof(healthLakeHttpService));
    }

    /// <inheritdoc />
    public async Task PutResourceAsync(string resourceType,
        string resourceId,
        string resourceJson,
        CancellationToken cancellationToken = default)
    {
        await PutResourceAsync(resourceType, resourceId, resourceJson, null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task PutResourceAsync<T>(string resourceType,
        string resourceId,
        T resource,
        CancellationToken cancellationToken = default) where T : Hl7.Fhir.Model.Base
    {
        if (resource == null)
            throw new ArgumentNullException(nameof(resource));

        // Use FHIR serializer for proper FHIR resource serialization
        var serializer = new FhirJsonSerializer(new SerializerSettings
        {
            Pretty = false,
            AppendNewLine = false
        });
        string json = serializer.SerializeToString(resource);

        await PutResourceAsync(resourceType, resourceId, json, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<T?> GetResourceAsync<T>(string resourceType,
        string resourceId,
        CancellationToken cancellationToken = default) where T : Hl7.Fhir.Model.Base
    {
        ValidateResourceType(resourceType);
        ValidateResourceId(resourceId);

        _logger.LogInformation("Starting HealthLake GET operation for {ResourceType}/{ResourceId}",
            resourceType, resourceId);

        await _concurrencySemaphore.WaitAsync(cancellationToken);
        try
        {
            // Build the GET request to HealthLake FHIR endpoint
            var endpoint
                = $"https://healthlake.{_config.Region}.amazonaws.com/datastore/{_config.DatastoreId}/r4/{resourceType}/{resourceId}";

            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);

            // Set Accept header for FHIR JSON response
            request.Headers.Add("Accept", "application/json");

            // Send the request with AWS credentials
            HttpResponseMessage response
                = await _healthLakeHttpService.SendSignedRequestAsync(request, cancellationToken);

            // Handle response and deserialize
            T resource = await HandleGetResponseAsync<T>(response, resourceType, resourceId,
                cancellationToken);

            _logger.LogInformation(
                "Successfully retrieved FHIR resource from HealthLake: {ResourceType}/{ResourceId}",
                resourceType, resourceId);

            return resource;
        }
        catch (HealthLakeException)
        {
            throw; // Re-throw HealthLake-specific exceptions
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error retrieving FHIR resource from HealthLake: {ResourceType}/{ResourceId}",
                resourceType, resourceId);
            throw new HealthLakeException(
                $"Failed to retrieve FHIR resource from HealthLake: {ex.Message}",
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
    public async Task<string?> GetResourceAsync(string resourceType,
        string resourceId,
        CancellationToken cancellationToken = default)
    {
        ValidateResourceType(resourceType);
        ValidateResourceId(resourceId);

        _logger.LogInformation("Starting HealthLake GET operation for {ResourceType}/{ResourceId}",
            resourceType, resourceId);

        await _concurrencySemaphore.WaitAsync(cancellationToken);
        try
        {
            // Build the GET request to HealthLake FHIR endpoint
            var endpoint
                = $"https://healthlake.{_config.Region}.amazonaws.com/datastore/{_config.DatastoreId}/r4/{resourceType}/{resourceId}";

            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);

            // Set Accept header for FHIR JSON response
            request.Headers.Add("Accept", "application/json");

            // Send the request with AWS credentials
            HttpResponseMessage response
                = await _healthLakeHttpService.SendSignedRequestAsync(request, cancellationToken);

            // Handle response based on status code
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("FHIR resource not found in HealthLake: {ResourceType}/{ResourceId}",
                    resourceType, resourceId);
                return null;
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new HealthLakeException(
                    "Authentication failed for HealthLake access",
                    resourceType,
                    resourceId,
                    HttpStatusCode.Unauthorized);
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                throw new HealthLakeAccessDeniedException(
                    $"Access denied to HealthLake datastore for resource {resourceType}/{resourceId}");
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                TimeSpan? retryAfter = response.Headers.RetryAfter?.Delta;
                throw new HealthLakeThrottlingException(
                    "HealthLake API rate limit exceeded",
                    retryAfter.HasValue ? DateTimeOffset.UtcNow.Add(retryAfter.Value) : null);
            }

            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new HealthLakeException(
                    $"HealthLake GET request failed with status {response.StatusCode}: {errorContent}",
                    resourceType,
                    resourceId,
                    response.StatusCode);
            }

            // Success - read and return the JSON string
            string json = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogInformation(
                "Successfully retrieved FHIR resource from HealthLake: {ResourceType}/{ResourceId}",
                resourceType, resourceId);

            return json;
        }
        catch (HealthLakeException)
        {
            throw; // Re-throw HealthLake-specific exceptions
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error retrieving FHIR resource from HealthLake: {ResourceType}/{ResourceId}",
                resourceType, resourceId);
            throw new HealthLakeException(
                $"Failed to retrieve FHIR resource from HealthLake: {ex.Message}",
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
    public async Task<Dictionary<string, string?>> GetResourcesAsync(
        IEnumerable<(string ResourceType, string ResourceId)> resourceRequests,
        CancellationToken cancellationToken = default)
    {
        if (resourceRequests == null)
            throw new ArgumentNullException(nameof(resourceRequests));

        List<(string ResourceType, string ResourceId)> requestList = resourceRequests.ToList();
        if (!requestList.Any())
            return new Dictionary<string, string?>();

        _logger.LogInformation("Starting batch HealthLake GET operation for {Count} resources",
            requestList.Count);

        var results = new Dictionary<string, string?>();
        IEnumerable<Task> tasks = requestList.Select(async req =>
        {
            var key = $"{req.ResourceType}/{req.ResourceId}";
            try
            {
                string? json = await GetResourceAsync(req.ResourceType, req.ResourceId, cancellationToken);
                lock (results)
                {
                    results[key] = json;
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

        _logger.LogInformation("Completed batch GET operation. Success: {SuccessCount}/{TotalCount}",
            results.Count(r => r.Value != null), requestList.Count);

        return results;
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, bool>> PutResourcesAsync(
        IEnumerable<(string ResourceType, string ResourceId, string ResourceJson)> resources,
        CancellationToken cancellationToken = default)
    {
        if (resources == null)
            throw new ArgumentNullException(nameof(resources));

        List<(string ResourceType, string ResourceId, string ResourceJson)> resourceList
            = resources.ToList();
        if (!resourceList.Any())
            return new Dictionary<string, bool>();

        _logger.LogInformation("Starting batch HealthLake PUT operation for {Count} resources",
            resourceList.Count);

        var results = new Dictionary<string, bool>();
        IEnumerable<Task> tasks = resourceList.Select(async resource =>
        {
            var key = $"{resource.ResourceType}/{resource.ResourceId}";
            try
            {
                await PutResourceAsync(resource.ResourceType, resource.ResourceId,
                    resource.ResourceJson, cancellationToken);
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

        int successCount = results.Values.Count(success => success);
        _logger.LogInformation(
            "Completed batch HealthLake PUT operation: {SuccessCount}/{TotalCount} successful",
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
    IReadOnlyList<string> IFhirDestinationService.GetSupportedResourceTypes()
    {
        return SupportedResourceTypes.ToList().AsReadOnly();
    }

    /// <inheritdoc />
    IReadOnlyCollection<string> IFhirSourceService.GetSupportedResourceTypes()
    {
        return SupportedResourceTypes;
    }

    /// <summary>
    ///     Gets the list of supported FHIR resource types
    /// </summary>
    public IReadOnlyList<string> GetSupportedResourceTypes()
    {
        return SupportedResourceTypes.ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<WriteResult> WriteResourceAsync(string resourceJson,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Parse resource to get type and ID
            using JsonDocument doc = JsonDocument.Parse(resourceJson);
            JsonElement root = doc.RootElement;

            if (!root.TryGetProperty("resourceType", out JsonElement resourceTypeElement))
                return new WriteResult
                {
                    Success = false,
                    ErrorMessage = "Resource JSON does not contain resourceType",
                    IsRetryable = false
                };

            string resourceType = resourceTypeElement.GetString() ?? string.Empty;

            if (!root.TryGetProperty("id", out JsonElement idElement))
                return new WriteResult
                {
                    Success = false,
                    ErrorMessage = "Resource JSON does not contain id",
                    IsRetryable = false
                };

            string resourceId = idElement.GetString() ?? string.Empty;

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

    /// <summary>
    ///     Writes a FHIR resource to HealthLake with optional version control
    /// </summary>
    /// <param name="resourceType">The FHIR resource type</param>
    /// <param name="resourceId">The unique identifier for the resource</param>
    /// <param name="resourceJson">The FHIR resource as JSON string</param>
    /// <param name="ifMatchVersion">Optional version for conditional update</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The new version of the resource</returns>
    public async Task<string?> PutResourceAsync(string resourceType,
        string resourceId,
        string resourceJson,
        string? ifMatchVersion,
        CancellationToken cancellationToken = default)
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
            var endpoint
                = $"https://healthlake.{_config.Region}.amazonaws.com/datastore/{_config.DatastoreId}/r4/{resourceType}/{resourceId}";

            using var request = new HttpRequestMessage(HttpMethod.Put, endpoint);

            // Create content and set exact Content-Type header without charset
            request.Content = new StringContent(resourceJson, Encoding.UTF8);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/fhir+json");

            // Add If-Match header for version control if provided
            if (!string.IsNullOrWhiteSpace(ifMatchVersion))
            {
                request.Headers.Add("If-Match", ifMatchVersion);
                _logger.LogDebug("Added If-Match header for version control: {Version}",
                    ifMatchVersion);
            }

            // Add validation level header (strict validation)
            request.Headers.Add("x-amz-fhir-validation-level", "strict");

            // Send the request with AWS credentials
            HttpResponseMessage response
                = await _healthLakeHttpService.SendSignedRequestAsync(request, cancellationToken);

            // Handle response and extract version
            string? version = await HandlePutResponseAsync(response, resourceType, resourceId,
                cancellationToken);

            _logger.LogInformation(
                "Successfully wrote FHIR resource to HealthLake: {ResourceType}/{ResourceId} Version: {Version}",
                resourceType, resourceId, version ?? "unknown");

            return version;
        }
        catch (HealthLakeException)
        {
            throw; // Re-throw HealthLake-specific exceptions
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error writing FHIR resource to HealthLake: {ResourceType}/{ResourceId}",
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

    /// <summary>
    ///     Handles the HTTP response from a PUT operation
    /// </summary>
    /// <param name="response">The HTTP response message</param>
    /// <param name="resourceType">The FHIR resource type</param>
    /// <param name="resourceId">The resource identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The version of the resource if available</returns>
    private async Task<string?> HandlePutResponseAsync(HttpResponseMessage response,
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken)
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
                string badRequestContent
                    = await response.Content.ReadAsStringAsync(cancellationToken);
                string? badRequestDetails = await ExtractOperationOutcomeAsync(badRequestContent);
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
                string conflictContent
                    = await response.Content.ReadAsStringAsync(cancellationToken);
                string? conflictDetails = await ExtractOperationOutcomeAsync(conflictContent);
                throw new HealthLakeConflictException(
                    resourceType,
                    resourceId,
                    conflictDetails ?? "Version conflict detected");

            case HttpStatusCode.TooManyRequests:
                TimeSpan? retryAfter = response.Headers.RetryAfter?.Delta;
                throw new HealthLakeThrottlingException(
                    "HealthLake API rate limit exceeded",
                    retryAfter.HasValue ? DateTimeOffset.UtcNow.Add(retryAfter.Value) : null);

            case HttpStatusCode.InternalServerError:
            case HttpStatusCode.BadGateway:
            case HttpStatusCode.ServiceUnavailable:
            case HttpStatusCode.GatewayTimeout:
                string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new HealthLakeException(
                    $"HealthLake service error: {response.StatusCode} - {errorContent}",
                    resourceType,
                    resourceId,
                    response.StatusCode);

            default:
                string unknownErrorContent
                    = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new HealthLakeException(
                    $"Unexpected HealthLake response: {response.StatusCode} - {unknownErrorContent}",
                    resourceType,
                    resourceId,
                    response.StatusCode);
        }
    }

    /// <summary>
    ///     Handles the HTTP response from a GET operation
    /// </summary>
    /// <typeparam name="T">The type to deserialize the resource to</typeparam>
    /// <param name="response">The HTTP response message</param>
    /// <param name="resourceType">The FHIR resource type</param>
    /// <param name="resourceId">The resource identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The deserialized resource</returns>
    private async Task<T> HandleGetResponseAsync<T>(HttpResponseMessage response,
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken) where T : class
    {
        switch (response.StatusCode)
        {
            case HttpStatusCode.OK:
                // Success - deserialize and return the resource
                string responseContent
                    = await response.Content.ReadAsStringAsync(cancellationToken);

                if (string.IsNullOrWhiteSpace(responseContent))
                    throw new HealthLakeException(
                        "HealthLake returned empty response for resource",
                        resourceType,
                        resourceId,
                        HttpStatusCode.OK);

                try
                {
                    // Use FHIR parser for proper deserialization of FHIR resources
                    var parser = new FhirJsonParser();
                    T? resource = parser.Parse(responseContent, typeof(T)) as T;

                    if (resource == null)
                        throw new HealthLakeException(
                            "Failed to deserialize HealthLake response to expected type",
                            resourceType,
                            resourceId);

                    return resource;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to deserialize HealthLake response for {ResourceType}/{ResourceId}",
                        resourceType, resourceId);
                    throw new HealthLakeException(
                        $"Failed to deserialize HealthLake response: {ex.Message}",
                        resourceType,
                        resourceId,
                        ex);
                }

            case HttpStatusCode.NotFound:
                throw new HealthLakeException(
                    $"Resource not found: {resourceType}/{resourceId}",
                    resourceType,
                    resourceId,
                    HttpStatusCode.NotFound);

            case HttpStatusCode.Unauthorized:
                throw new HealthLakeException(
                    "Authentication failed for HealthLake access",
                    resourceType,
                    resourceId,
                    HttpStatusCode.Unauthorized);

            case HttpStatusCode.Forbidden:
                throw new HealthLakeAccessDeniedException(
                    $"Access denied to HealthLake datastore for resource {resourceType}/{resourceId}");

            case HttpStatusCode.TooManyRequests:
                TimeSpan? retryAfter = response.Headers.RetryAfter?.Delta;
                throw new HealthLakeThrottlingException(
                    "HealthLake API rate limit exceeded",
                    retryAfter.HasValue ? DateTimeOffset.UtcNow.Add(retryAfter.Value) : null);

            case HttpStatusCode.InternalServerError:
            case HttpStatusCode.BadGateway:
            case HttpStatusCode.ServiceUnavailable:
            case HttpStatusCode.GatewayTimeout:
                string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new HealthLakeException(
                    $"HealthLake service error: {response.StatusCode} - {errorContent}",
                    resourceType,
                    resourceId,
                    response.StatusCode);

            default:
                string unknownErrorContent
                    = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new HealthLakeException(
                    $"Unexpected HealthLake response: {response.StatusCode} - {unknownErrorContent}",
                    resourceType,
                    resourceId,
                    response.StatusCode);
        }
    }

    /// <summary>
    ///     Extracts error details from a FHIR OperationOutcome response
    /// </summary>
    /// <param name="responseContent">The response content as string</param>
    /// <returns>Extracted error message or null</returns>
    private Task<string?> ExtractOperationOutcomeAsync(string responseContent)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(responseContent))
                return Task.FromResult<string?>(null);

            using JsonDocument doc = JsonDocument.Parse(responseContent);
            JsonElement root = doc.RootElement;

            // Check if this is an OperationOutcome resource
            if (root.TryGetProperty("resourceType", out JsonElement resourceType) &&
                resourceType.GetString() == "OperationOutcome")
                // Extract issue details
                if (root.TryGetProperty("issue", out JsonElement issues) &&
                    issues.ValueKind == JsonValueKind.Array)
                {
                    var issueMessages = new List<string>();
                    foreach (JsonElement issue in issues.EnumerateArray())
                        if (issue.TryGetProperty("diagnostics", out JsonElement diagnostics))
                            issueMessages.Add(diagnostics.GetString() ?? string.Empty);
                        else if (issue.TryGetProperty("details", out JsonElement details) &&
                                 details.TryGetProperty("text", out JsonElement text))
                            issueMessages.Add(text.GetString() ?? string.Empty);

                    if (issueMessages.Any())
                        return Task.FromResult<string?>(string.Join("; ", issueMessages));
                }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse OperationOutcome from response");
        }

        return Task.FromResult<string?>(null);
    }

    /// <summary>
    ///     Validates that the resource type is supported
    /// </summary>
    /// <param name="resourceType">The resource type to validate</param>
    /// <exception cref="ArgumentException">Thrown when resource type is invalid</exception>
    private static void ValidateResourceType(string resourceType)
    {
        if (string.IsNullOrWhiteSpace(resourceType))
            throw new ArgumentException("Resource type cannot be null or empty.",
                nameof(resourceType));

        if (!SupportedResourceTypes.Contains(resourceType))
            throw new ArgumentException(
                $"Resource type '{resourceType}' is not supported. Supported types: {string.Join(", ", SupportedResourceTypes)}",
                nameof(resourceType));
    }

    /// <summary>
    ///     Determines if an HTTP status code indicates a retryable error
    /// </summary>
    /// <param name="statusCode">The HTTP status code</param>
    /// <returns>True if the error is retryable</returns>
    private static bool IsRetryableStatusCode(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.TooManyRequests ||
               statusCode == HttpStatusCode.ServiceUnavailable ||
               statusCode == HttpStatusCode.GatewayTimeout ||
               statusCode == HttpStatusCode.RequestTimeout;
    }

    /// <summary>
    ///     Validates that the resource ID is not null or empty
    /// </summary>
    /// <param name="resourceId">The resource ID to validate</param>
    /// <exception cref="ArgumentException">Thrown when resource ID is invalid</exception>
    private static void ValidateResourceId(string resourceId)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
            throw new ArgumentException("Resource ID cannot be null or empty.", nameof(resourceId));
    }

    /// <summary>
    ///     Validates that the resource JSON is valid
    /// </summary>
    /// <param name="resourceJson">The JSON to validate</param>
    /// <exception cref="ArgumentException">Thrown when JSON is invalid</exception>
    private static void ValidateResourceJson(string resourceJson)
    {
        if (string.IsNullOrWhiteSpace(resourceJson))
            throw new ArgumentException("Resource JSON cannot be null or empty.",
                nameof(resourceJson));

        // Basic JSON validation
        try
        {
            JsonDocument.Parse(resourceJson);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Invalid JSON format: {ex.Message}", nameof(resourceJson),
                ex);
        }
    }

    /// <summary>
    ///     Disposes resources used by the service
    /// </summary>
    public void Dispose()
    {
        _concurrencySemaphore?.Dispose();
        _healthLakeClient?.Dispose();
    }
}