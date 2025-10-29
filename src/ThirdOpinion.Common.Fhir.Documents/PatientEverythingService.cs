using System.Net.Http.Headers;
using System.Text.Json;
using ThirdOpinion.Common.Aws.HealthLake.Http;
using ThirdOpinion.Common.Aws.HealthLake.Configuration;
using ThirdOpinion.Common.Fhir.Documents.Exceptions;
using ThirdOpinion.Common.Fhir.Documents.Models;
using ThirdOpinion.Common.Aws.HealthLake;
using ThirdOpinion.Common.Logging;
using ThirdOpinion.Common.Misc.RateLimiting;
using ThirdOpinion.Common.Misc.Retry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ThirdOpinion.Common.Fhir.Documents;

/// <summary>
/// Service for retrieving Patient/$everything Bundle responses from HealthLake
/// </summary>
public class PatientEverythingService : IPatientEverythingService
{
    private readonly HealthLakeConfig _config;
    private readonly ILogger<PatientEverythingService> _logger;
    private readonly ICorrelationIdProvider _correlationIdProvider;
    private readonly IRateLimiterService _rateLimiterService;
    private readonly IRetryPolicyService _retryPolicyService;
    private readonly IHealthLakeHttpService _healthLakeHttpService;

    private readonly SemaphoreSlim _concurrencySemaphore = new(10, 10);

    public PatientEverythingService(
        IOptions<HealthLakeConfig> config,
        ILogger<PatientEverythingService> logger,
        ICorrelationIdProvider correlationIdProvider,
        IRateLimiterService rateLimiterService,
        IRetryPolicyService retryPolicyService,
        IHealthLakeHttpService healthLakeHttpService)
    {
        _config = config.Value;
        _logger = logger;
        _correlationIdProvider = correlationIdProvider;
        _rateLimiterService = rateLimiterService;
        _retryPolicyService = retryPolicyService;
        _healthLakeHttpService = healthLakeHttpService;
    }

    public async Task<IReadOnlyList<DocumentReferenceData>> GetPatientDocumentReferencesAsync(
        string patientId,
        CancellationToken cancellationToken = default)
    {
        var correlationId = _correlationIdProvider.GetCorrelationId();
        _logger.LogInformation("Starting retrieval of DocumentReference resources for patient: {PatientId} [CorrelationId: {CorrelationId}]",
            patientId, correlationId);

        var allDocuments = new List<DocumentReferenceData>();
        string? nextPageUrl = null;

        try
        {
            do
            {
                var bundle = await GetPatientEverythingBundleAsync(patientId, nextPageUrl, cancellationToken);

                if (!bundle.IsValidSearchsetBundle())
                {
                    throw new DocumentDownloadException(
                        $"Invalid Bundle response: expected searchset Bundle, got {bundle.Type}");
                }

                // Log bundle total information (only on first page)
                if (nextPageUrl == null && bundle.Total.HasValue)
                {
                    _logger.LogInformation("Bundle reports total of {BundleTotal} resources available for patient: {PatientId} [CorrelationId: {CorrelationId}]",
                        bundle.Total.Value, patientId, correlationId);
                }

                var documents = bundle.GetDocumentReferences();
                allDocuments.AddRange(documents);

                _logger.LogDebug("Retrieved {Count} DocumentReference resources from page (Bundle entries: {BundleEntries}), total DocumentReferences so far: {Total} [CorrelationId: {CorrelationId}]",
                    documents.Count, bundle.Entry.Count, allDocuments.Count, correlationId);

                nextPageUrl = bundle.GetNextPageUrl();

            } while (!string.IsNullOrEmpty(nextPageUrl));

            _logger.LogInformation("Successfully retrieved {TotalCount} DocumentReference resources for patient: {PatientId} [CorrelationId: {CorrelationId}]",
                allDocuments.Count, patientId, correlationId);

            return allDocuments.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve DocumentReference resources for patient: {PatientId} [CorrelationId: {CorrelationId}]",
                patientId, correlationId);
            throw;
        }
    }

    public async Task<BundleData> GetPatientEverythingBundleAsync(
        string patientId,
        string? pageUrl = null,
        CancellationToken cancellationToken = default)
    {
        var correlationId = _correlationIdProvider.GetCorrelationId();

        await _concurrencySemaphore.WaitAsync(cancellationToken);
        try
        {
            // AWS HealthLake accepts $ in the URL, but signature calculation requires %24
            var endpoint = pageUrl ??
                $"https://healthlake.{_config.Region}.amazonaws.com/datastore/{_config.DatastoreId}/r4/Patient/{patientId}/$everything?_type=DocumentReference";

            _logger.LogInformation("Using HealthLake configuration - Region: {Region}, DatastoreId: {DatastoreId}",
                _config.Region, _config.DatastoreId);

            _logger.LogDebug("Sending Patient/$everything request to: {Endpoint} [CorrelationId: {CorrelationId}]",
                endpoint, correlationId);

            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);

            // Add correlation ID header
            request.Headers.Add("X-Correlation-ID", correlationId);

            // Set Accept header for FHIR JSON
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/fhir+json"));

            // Get retry policy for HealthLake service
            var retryPolicy = _retryPolicyService.GetCombinedPolicy("HealthLake");

            // Execute request with retry policy
            var response = await retryPolicy.ExecuteAsync(async () =>
            {
                // Apply rate limiting
                var rateLimiter = _rateLimiterService.GetRateLimiter("HealthLake");
                await rateLimiter.WaitAsync(cancellationToken);

                // Clone request for potential retries
                var clonedRequest = await _healthLakeHttpService.CloneHttpRequestAsync(request);

                // Send the request with AWS credentials
                return await _healthLakeHttpService.SendSignedRequestAsync(clonedRequest, cancellationToken);
            });

            // Handle response and parse Bundle
            var bundle = await HandleBundleResponseAsync(response, patientId, cancellationToken);

            _logger.LogDebug("Successfully retrieved Bundle for patient: {PatientId}, entries: {EntryCount} [CorrelationId: {CorrelationId}]",
                patientId, bundle.Entry.Count, correlationId);

            return bundle;
        }
        finally
        {
            _concurrencySemaphore.Release();
        }
    }


    private async Task<BundleData> HandleBundleResponseAsync(HttpResponseMessage response, string patientId, CancellationToken cancellationToken)
    {
        var correlationId = _correlationIdProvider.GetCorrelationId();

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("HealthLake request failed with status: {StatusCode}, Content: {Content} [CorrelationId: {CorrelationId}]",
                response.StatusCode, errorContent, correlationId);

            throw new DocumentDownloadException(
                $"HealthLake request failed with status {response.StatusCode}: {errorContent}");
        }

        try
        {
            var bundleJson = await response.Content.ReadAsStringAsync(cancellationToken);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var bundle = JsonSerializer.Deserialize<BundleData>(bundleJson, options);

            if (bundle == null)
            {
                throw new DocumentDownloadException("Failed to deserialize Bundle response from HealthLake");
            }

            _logger.LogDebug("Successfully parsed Bundle response: Type={Type}, Total={Total}, Entries={EntryCount} [CorrelationId: {CorrelationId}]",
                bundle.Type, bundle.Total, bundle.Entry.Count, correlationId);

            return bundle;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize Bundle response for patient: {PatientId} [CorrelationId: {CorrelationId}]",
                patientId, correlationId);
            throw new DocumentDownloadException("Failed to parse Bundle response from HealthLake", ex);
        }
    }
}