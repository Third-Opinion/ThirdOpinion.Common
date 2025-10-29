using System.Text.RegularExpressions;
using ThirdOpinion.Common.Fhir.Documents.Models;
using ThirdOpinion.Common.Logging;
using Microsoft.Extensions.Logging;

namespace ThirdOpinion.Common.Fhir.Documents;

/// <summary>
/// Service for extracting metadata from DocumentReference resources and generating S3 tags
/// </summary>
public class MetadataExtractorService : IMetadataExtractorService
{
    private readonly ILogger<MetadataExtractorService> _logger;
    private readonly ICorrelationIdProvider _correlationIdProvider;

    // S3 tag restrictions: 128 Unicode characters max, specific character set
    // Only characters allowed by S3: letters, numbers, space, + - = . _ : / @
    private static readonly Regex S3TagValueRegex = new(@"[^a-zA-Z0-9\+\-=\._:\/@\s]", RegexOptions.Compiled);

    public MetadataExtractorService(
        ILogger<MetadataExtractorService> logger,
        ICorrelationIdProvider correlationIdProvider)
    {
        _logger = logger;
        _correlationIdProvider = correlationIdProvider;
    }

    public S3TagSet ExtractMetadataToS3Tags(DocumentReferenceData documentReference)
    {
        return ExtractMetadataToS3Tags(documentReference, null);
    }

    public S3TagSet ExtractMetadataToS3Tags(DocumentReferenceData documentReference, string? resolvedPracticeName)
    {
        return ExtractMetadataToS3Tags(documentReference, resolvedPracticeName, null);
    }

    public S3TagSet ExtractMetadataToS3Tags(DocumentReferenceData documentReference, string? resolvedPracticeName, Attachment? currentAttachment)
    {
        var correlationId = _correlationIdProvider.GetCorrelationId();

        _logger.LogDebug("Extracting metadata from DocumentReference {DocumentId} [CorrelationId: {CorrelationId}]",
            documentReference.Id, correlationId);

        var tagSet = new S3TagSet();

        try
        {
            // Extract document type from type.coding
            var documentType = ExtractDocumentType(documentReference);
            if (!string.IsNullOrEmpty(documentType))
            {
                tagSet.AddTag("document.type", SanitizeTagValue(documentType, 256));
            }

            // Extract encounter reference
            var encounterRef = ExtractEncounterReference(documentReference);
            if (!string.IsNullOrEmpty(encounterRef))
            {
                tagSet.AddTag("encounter.reference", SanitizeTagValue(encounterRef));
            }

            // Combine meta.lastUpdated and meta.versionId
            if (documentReference.Meta != null)
            {
                var lastUpdated = documentReference.Meta.LastUpdated ?? "";
                var versionId = documentReference.Meta.VersionId ?? "";
                if (!string.IsNullOrEmpty(lastUpdated) || !string.IsNullOrEmpty(versionId))
                {
                    // Prepend 'v' to version ID if it exists and doesn't already start with 'v'
                    var formattedVersionId = !string.IsNullOrEmpty(versionId) && !versionId.StartsWith("v", StringComparison.OrdinalIgnoreCase)
                        ? $"v{versionId}"
                        : versionId;
                    var metaValue = $"{lastUpdated}|{formattedVersionId}";
                    tagSet.AddTag("meta", SanitizeTagValue(metaValue));
                }
            }

            // Extract status
            if (!string.IsNullOrEmpty(documentReference.Status))
            {
                tagSet.AddTag("status", SanitizeTagValue(documentReference.Status));
            }

            // Extract document date
            if (!string.IsNullOrEmpty(documentReference.Date))
            {
                tagSet.AddTag("documentreference.date", SanitizeTagValue(documentReference.Date));
                _logger.LogDebug("Added documentreference.date tag with value: {Date} [CorrelationId: {CorrelationId}]",
                    documentReference.Date, correlationId);
            }

            // Add DocumentReference ID with prefix for tracking
            tagSet.AddTag("documentreference.id", SanitizeTagValue($"DocumentReference/{documentReference.Id}"));

            // Extract patient ID if available
            var patientId = documentReference.GetPatientId();
            if (!string.IsNullOrEmpty(patientId))
            {
                tagSet.AddTag("patient.id", SanitizeTagValue(patientId));
            }

            // Extract practice info if available
            var practiceInfo = documentReference.GetPracticeInfo();
            if (practiceInfo != null && !string.IsNullOrEmpty(practiceInfo.Id))
            {
                // Use resolved practice name if provided, otherwise fall back to practice info name
                var practiceNameToUse = resolvedPracticeName ?? practiceInfo.Name;

                // Combine practice.id and practice.name into single field
                var practiceValue = !string.IsNullOrEmpty(practiceNameToUse)
                    ? $"{practiceInfo.Id} {practiceNameToUse}"
                    : practiceInfo.Id;

                tagSet.AddTag("practice.id", SanitizeTagValue(practiceValue));

                _logger.LogDebug("Added practice.id tag with value: {PracticeValue} (resolved name: {WasResolved}) [CorrelationId: {CorrelationId}]",
                    practiceValue, resolvedPracticeName != null, correlationId);
            }

            // Extract binary ID if current attachment is provided
            if (currentAttachment != null && !currentAttachment.IsEmbeddedContent)
            {
                try
                {
                    var binaryId = currentAttachment.GetBinaryId();
                    if (!string.IsNullOrEmpty(binaryId))
                    {
                        // If the binaryId already starts with "Binary/", use as-is, otherwise prepend
                        var binaryIdValue = binaryId.StartsWith("Binary/") ? binaryId : $"Binary/{binaryId}";
                        tagSet.AddTag("binary.id", SanitizeTagValue(binaryIdValue));
                        _logger.LogDebug("Added binary.id tag with value: {BinaryIdValue} [CorrelationId: {CorrelationId}]",
                            binaryIdValue, correlationId);
                    }
                    else
                    {
                        _logger.LogDebug("BinaryId was null or empty from GetBinaryId(). Attachment URL: {AttachmentUrl} [CorrelationId: {CorrelationId}]",
                            currentAttachment?.Url ?? "null", correlationId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to extract binary ID from attachment. URL: {AttachmentUrl} [CorrelationId: {CorrelationId}]",
                        currentAttachment?.Url ?? "null", correlationId);
                }
            }
            else
            {
                _logger.LogDebug("currentAttachment is null or IsEmbeddedContent. currentAttachment != null: {IsNotNull}, IsEmbeddedContent: {IsEmbedded} [CorrelationId: {CorrelationId}]",
                    currentAttachment != null, currentAttachment?.IsEmbeddedContent ?? false, correlationId);
            }

            _logger.LogDebug("Extracted {TagCount} metadata tags from DocumentReference {DocumentId} [CorrelationId: {CorrelationId}]",
                tagSet.Tags.Count, documentReference.Id, correlationId);

            return tagSet;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting metadata from DocumentReference {DocumentId}. Error Type: {ErrorType}, Message: {ErrorMessage}, StackTrace: {StackTrace} [CorrelationId: {CorrelationId}]",
                documentReference.Id,
                ex.GetType().Name,
                ex.Message,
                ex.StackTrace,
                correlationId);

            // Log inner exception if present
            if (ex.InnerException != null)
            {
                _logger.LogError(ex.InnerException, "Inner Exception - Type: {InnerErrorType}, Message: {InnerErrorMessage} [CorrelationId: {CorrelationId}]",
                    ex.InnerException.GetType().Name,
                    ex.InnerException.Message,
                    correlationId);
            }

            // Return basic tags even if extraction fails
            tagSet.AddTag("documentreference.id", SanitizeTagValue($"DocumentReference/{documentReference.Id}"));
            tagSet.AddTag("extraction.error", "true");
            tagSet.AddTag("error.type", SanitizeTagValue(ex.GetType().Name));
            tagSet.AddTag("error.message", SanitizeTagValue(ex.Message, 256));
            return tagSet;
        }
    }

    public string? ExtractDocumentCategory(DocumentReferenceData documentReference)
    {
        var correlationId = _correlationIdProvider.GetCorrelationId();

        if (documentReference.Category?.Count == 0)
        {
            _logger.LogDebug("No category found in DocumentReference {DocumentId} [CorrelationId: {CorrelationId}]",
                documentReference.Id, correlationId);
            return null;
        }

        var categories = new List<string>();

        foreach (var category in documentReference.Category)
        {
            foreach (var coding in category.Coding)
            {
                if (!string.IsNullOrEmpty(coding.Code))
                {
                    categories.Add(coding.Code);
                }
            }
        }

        if (categories.Count == 0)
        {
            _logger.LogDebug("No category codes found in DocumentReference {DocumentId} [CorrelationId: {CorrelationId}]",
                documentReference.Id, correlationId);
            return null;
        }

        if (categories.Count > 1)
        {
            _logger.LogWarning("Multiple categories found in DocumentReference {DocumentId}: {Categories}. Using first one. [CorrelationId: {CorrelationId}]",
                documentReference.Id, string.Join(", ", categories), correlationId);
        }

        return categories.First();
    }

    public string? ExtractDocumentType(DocumentReferenceData documentReference)
    {
        var correlationId = _correlationIdProvider.GetCorrelationId();

        try
        {
            if (documentReference.Type?.Coding == null || documentReference.Type.Coding.Count == 0)
            {
                _logger.LogDebug("No type found in DocumentReference {DocumentId} [CorrelationId: {CorrelationId}]",
                    documentReference.Id, correlationId);
                return null;
            }

            var types = new List<string>();

            foreach (var coding in documentReference.Type.Coding)
            {
                // Format as "Display(Code)" if both are present, otherwise use what's available
                string typeEntry;
                if (!string.IsNullOrEmpty(coding.Display) && !string.IsNullOrEmpty(coding.Code))
                {
                    typeEntry = $"{coding.Display}({coding.Code})";
                }
                else if (!string.IsNullOrEmpty(coding.Display))
                {
                    typeEntry = coding.Display;
                }
                else if (!string.IsNullOrEmpty(coding.Code))
                {
                    typeEntry = coding.Code;
                }
                else
                {
                    continue; // Skip empty entries
                }

                types.Add(typeEntry);
            }

            if (types.Count == 0)
            {
                _logger.LogDebug("No type codes found in DocumentReference {DocumentId} [CorrelationId: {CorrelationId}]",
                    documentReference.Id, correlationId);
                return null;
            }

            // Join multiple types with colon
            var result = string.Join(":", types);

            _logger.LogDebug("Extracted document type '{Type}' from DocumentReference {DocumentId} [CorrelationId: {CorrelationId}]",
                result, documentReference.Id, correlationId);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting document type from DocumentReference {DocumentId} [CorrelationId: {CorrelationId}]",
                documentReference.Id, correlationId);
            return null;
        }
    }

    public string? ExtractEncounterReference(DocumentReferenceData documentReference)
    {
        var correlationId = _correlationIdProvider.GetCorrelationId();

        if (documentReference.Context?.Encounter == null || documentReference.Context.Encounter.Count == 0)
        {
            _logger.LogDebug("No encounter context found in DocumentReference {DocumentId} [CorrelationId: {CorrelationId}]",
                documentReference.Id, correlationId);
            return null;
        }

        var encounters = documentReference.Context.Encounter
            .Where(e => !string.IsNullOrEmpty(e.Reference))
            .Select(e => e.Reference!)
            .ToList();

        if (encounters.Count == 0)
        {
            _logger.LogDebug("No encounter references found in DocumentReference {DocumentId} [CorrelationId: {CorrelationId}]",
                documentReference.Id, correlationId);
            return null;
        }

        if (encounters.Count > 1)
        {
            _logger.LogWarning("Multiple encounter references found in DocumentReference {DocumentId}: {Encounters}. Using first one. [CorrelationId: {CorrelationId}]",
                documentReference.Id, string.Join(", ", encounters), correlationId);
        }

        return encounters.First();
    }

    public string SanitizeTagValue(string? tagValue, int maxLength = 128)
    {
        if (string.IsNullOrEmpty(tagValue))
        {
            return "unknown";
        }

        // Replace invalid characters with underscores
        var sanitized = S3TagValueRegex.Replace(tagValue, "_");

        // Trim whitespace
        sanitized = sanitized.Trim();

        // Ensure length is within specified limit (default S3 limit is 128 characters)
        if (sanitized.Length > maxLength)
        {
            sanitized = sanitized.Substring(0, maxLength - 3) + "...";
        }

        // Ensure it's not empty after sanitization
        if (string.IsNullOrEmpty(sanitized))
        {
            return "unknown";
        }

        return sanitized;
    }

    public string SanitizeTagValue(string? tagValue)
    {
        return SanitizeTagValue(tagValue, 128);
    }
}