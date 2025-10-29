using System.Net.Http.Headers;
using ThirdOpinion.Common.Aws;
using ThirdOpinion.Common.Aws.HealthLake.Configuration;
using ThirdOpinion.Common.Aws.HealthLake;
using ThirdOpinion.Common.Aws.HealthLake.Http;
using ThirdOpinion.Common.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ThirdOpinion.Common.Fhir.Documents;

/// <summary>
/// Test service to verify different HealthLake endpoints with the same signature logic
/// </summary>
public class HealthLakeEndpointTester
{
    private readonly HealthLakeConfig _config;
    private readonly ILogger<HealthLakeEndpointTester> _logger;
    private readonly IHealthLakeHttpService _healthLakeHttpService;
    private readonly ICorrelationIdProvider _correlationIdProvider;

    public HealthLakeEndpointTester(
        IOptions<HealthLakeConfig> config,
        ILogger<HealthLakeEndpointTester> logger,
        IHealthLakeHttpService healthLakeHttpService,
        ICorrelationIdProvider correlationIdProvider)
    {
        _config = config.Value;
        _logger = logger;
        _healthLakeHttpService = healthLakeHttpService;
        _correlationIdProvider = correlationIdProvider;
    }

    public async Task TestMultipleEndpointsAsync(string patientId, CancellationToken cancellationToken = default)
    {
        var correlationId = _correlationIdProvider.GetCorrelationId();
        _logger.LogInformation("Starting endpoint tests for HealthLake [CorrelationId: {CorrelationId}]", correlationId);

        var endpoints = new[]
        {
            // Test basic metadata endpoint
            ($"https://healthlake.{_config.Region}.amazonaws.com/datastore/{_config.DatastoreId}/r4/metadata", "metadata"),

            // Test export endpoint (which was working in curl)
            ($"https://healthlake.{_config.Region}.amazonaws.com/datastore/{_config.DatastoreId}/r4/$export", "$export"),

            // Test Patient resource directly
            ($"https://healthlake.{_config.Region}.amazonaws.com/datastore/{_config.DatastoreId}/r4/Patient/{patientId}", "Patient/{id}"),

            // Test Patient/$everything endpoint (the failing one)
            ($"https://healthlake.{_config.Region}.amazonaws.com/datastore/{_config.DatastoreId}/r4/Patient/{patientId}/$everything", "Patient/{id}/$everything"),

            // Test Patient/$everything with _type parameter
            ($"https://healthlake.{_config.Region}.amazonaws.com/datastore/{_config.DatastoreId}/r4/Patient/{patientId}/$everything?_type=DocumentReference", "Patient/{id}/$everything?_type=DocumentReference")
        };

        foreach (var (endpoint, description) in endpoints)
        {
            try
            {
                _logger.LogInformation("Testing endpoint: {Description} - {Endpoint}", description, endpoint);

                using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
                request.Headers.Add("X-Correlation-ID", correlationId);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/fhir+json"));

                var response = await _healthLakeHttpService.SendSignedRequestAsync(request, cancellationToken);

                _logger.LogInformation("Endpoint {Description} returned: {StatusCode} - {ReasonPhrase}",
                    description, response.StatusCode, response.ReasonPhrase);

                if (!response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("Endpoint {Description} error content: {Content}", description, content);
                }
                else
                {
                    _logger.LogInformation("✓ Endpoint {Description} succeeded!", description);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "✗ Endpoint {Description} failed with exception", description);
            }
        }
    }
}