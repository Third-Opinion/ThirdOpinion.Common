using System.Text.Json;
using Microsoft.Extensions.Logging;
using ThirdOpinion.Common.Fhir.Documents.Exceptions;
using ThirdOpinion.Common.Fhir.Documents.Models;
using ThirdOpinion.Common.Logging;

namespace ThirdOpinion.Common.Fhir.Documents;

/// <summary>
///     Service for parsing FHIR Bundle resources and extracting DocumentReference data
/// </summary>
public class BundleParserService : IBundleParserService
{
    private readonly ICorrelationIdProvider _correlationIdProvider;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };

    private readonly ILogger<BundleParserService> _logger;

    public BundleParserService(
        ILogger<BundleParserService> logger,
        ICorrelationIdProvider correlationIdProvider)
    {
        _logger = logger;
        _correlationIdProvider = correlationIdProvider;
    }

    public BundleData ParseBundle(string bundleJson)
    {
        string correlationId = _correlationIdProvider.GetCorrelationId();

        if (string.IsNullOrWhiteSpace(bundleJson))
            throw new DocumentDownloadException("Bundle JSON cannot be null or empty");

        try
        {
            _logger.LogDebug(
                "Parsing Bundle JSON (length: {Length}) [CorrelationId: {CorrelationId}]",
                bundleJson.Length, correlationId);

            var bundle = JsonSerializer.Deserialize<BundleData>(bundleJson, _jsonOptions);

            if (bundle == null)
                throw new DocumentDownloadException("Failed to deserialize Bundle JSON");

            _logger.LogDebug(
                "Successfully parsed Bundle: Type={Type}, Total={Total}, Entries={EntryCount} [CorrelationId: {CorrelationId}]",
                bundle.Type, bundle.Total, bundle.Entry.Count, correlationId);

            // Validate Bundle structure
            if (!ValidateSearchsetBundle(bundle))
                throw new DocumentDownloadException(
                    $"Invalid Bundle: expected resourceType='Bundle' and type='searchset', got resourceType='{bundle.ResourceType}' and type='{bundle.Type}'");

            // Process DocumentReference entries and log any issues
            List<DocumentReferenceData>
                documentReferences = ProcessDocumentReferenceEntries(bundle);

            _logger.LogInformation(
                "Extracted {Count} DocumentReference resources from Bundle [CorrelationId: {CorrelationId}]",
                documentReferences.Count, correlationId);

            return bundle;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Bundle JSON [CorrelationId: {CorrelationId}]",
                correlationId);
            throw new DocumentDownloadException("Invalid Bundle JSON format", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error parsing Bundle [CorrelationId: {CorrelationId}]",
                correlationId);
            throw;
        }
    }

    public bool ValidateSearchsetBundle(BundleData bundle)
    {
        return bundle.ResourceType == "Bundle" && bundle.Type == "searchset";
    }

    public PracticeInfo? ExtractPracticeInfo(DocumentReferenceData documentReference)
    {
        string correlationId = _correlationIdProvider.GetCorrelationId();

        try
        {
            Extension? practiceExtension = documentReference.Extension
                .FirstOrDefault(e =>
                    e.Url == "https://fhir.athena.io/StructureDefinition/ah-practice");

            if (practiceExtension?.ValueReference?.Reference == null)
            {
                _logger.LogWarning(
                    "No practice extension found in DocumentReference: {DocumentId} [CorrelationId: {CorrelationId}]",
                    documentReference.Id, correlationId);
                return null;
            }

            string? practiceReference = practiceExtension.ValueReference.Reference;
            _logger.LogDebug(
                "Found practice reference: {Reference} [CorrelationId: {CorrelationId}]",
                practiceReference, correlationId);

            // Parse reference like "Organization/a-1.Practice-15454"
            string[]? parts = practiceReference.Split('/').LastOrDefault()?.Split('-');

            if (parts?.Length >= 2)
            {
                string practiceId = parts[1];

                _logger.LogDebug(
                    "Extracted practice ID: {PracticeId} from reference: {Reference} [CorrelationId: {CorrelationId}]",
                    practiceId, practiceReference, correlationId);

                return new PracticeInfo
                {
                    Id = practiceId,
                    Name = "Unknown" // Will be resolved by ResolvePracticeNameAsync
                };
            }

            _logger.LogWarning(
                "Failed to parse practice ID from reference: {Reference} [CorrelationId: {CorrelationId}]",
                practiceReference, correlationId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error extracting practice info from DocumentReference: {DocumentId} [CorrelationId: {CorrelationId}]",
                documentReference.Id, correlationId);
            return null;
        }
    }

    public async Task<string> ResolvePracticeNameAsync(string practiceId,
        CancellationToken cancellationToken = default)
    {
        string correlationId = _correlationIdProvider.GetCorrelationId();

        _logger.LogDebug(
            "Resolving practice name for ID: {PracticeId} [CorrelationId: {CorrelationId}]",
            practiceId, correlationId);

        // For now, use the practice ID as the name
        // TODO: Implement proper practice name mapping from configuration or database
        string practiceName = practiceId;

        _logger.LogDebug(
            "Resolved practice name: {PracticeName} for ID: {PracticeId} [CorrelationId: {CorrelationId}]",
            practiceName, practiceId, correlationId);

        // TODO: Future enhancement - implement actual Organization lookup from HealthLake
        // This would require additional HealthLake API calls to resolve Organization resources

        return await Task.FromResult(practiceName);
    }

    private List<DocumentReferenceData> ProcessDocumentReferenceEntries(BundleData bundle)
    {
        string correlationId = _correlationIdProvider.GetCorrelationId();
        var documentReferences = new List<DocumentReferenceData>();

        foreach (BundleEntry entry in bundle.Entry)
            try
            {
                if (entry.Resource == null)
                {
                    _logger.LogWarning(
                        "Bundle entry has null resource [CorrelationId: {CorrelationId}]",
                        correlationId);
                    continue;
                }

                // The DocumentReferenceData model handles the resourceType check
                if (string.IsNullOrEmpty(entry.Resource.Id))
                {
                    _logger.LogWarning(
                        "DocumentReference has empty ID [CorrelationId: {CorrelationId}]",
                        correlationId);
                    continue;
                }

                documentReferences.Add(entry.Resource);

                _logger.LogDebug(
                    "Processed DocumentReference: {DocumentId}, Status: {Status}, Content Count: {ContentCount} [CorrelationId: {CorrelationId}]",
                    entry.Resource.Id, entry.Resource.Status, entry.Resource.Content.Count,
                    correlationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error processing Bundle entry for DocumentReference [CorrelationId: {CorrelationId}]",
                    correlationId);
                // Continue processing other entries
            }

        return documentReferences;
    }
}