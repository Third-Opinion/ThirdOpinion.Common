using ThirdOpinion.Common.Fhir.Documents.Models;

namespace ThirdOpinion.Common.Fhir.Documents;

/// <summary>
/// Service for extracting metadata from DocumentReference resources and generating S3 tags
/// </summary>
public interface IMetadataExtractorService
{
    /// <summary>
    /// Extracts metadata from a DocumentReference and converts it to S3 tags
    /// </summary>
    /// <param name="documentReference">The DocumentReference resource</param>
    /// <returns>S3TagSet with extracted metadata</returns>
    S3TagSet ExtractMetadataToS3Tags(DocumentReferenceData documentReference);

    /// <summary>
    /// Extracts metadata from a DocumentReference and converts it to S3 tags with resolved practice name
    /// </summary>
    /// <param name="documentReference">The DocumentReference resource</param>
    /// <param name="resolvedPracticeName">The resolved practice name to use instead of the default from the document</param>
    /// <returns>S3TagSet with extracted metadata</returns>
    S3TagSet ExtractMetadataToS3Tags(DocumentReferenceData documentReference, string? resolvedPracticeName);

    /// <summary>
    /// Extracts metadata from a DocumentReference and converts it to S3 tags with resolved practice name and current attachment
    /// </summary>
    /// <param name="documentReference">The DocumentReference resource</param>
    /// <param name="resolvedPracticeName">The resolved practice name to use instead of the default from the document</param>
    /// <param name="currentAttachment">The current attachment being processed (for binary.id extraction)</param>
    /// <returns>S3TagSet with extracted metadata</returns>
    S3TagSet ExtractMetadataToS3Tags(DocumentReferenceData documentReference, string? resolvedPracticeName, Attachment? currentAttachment);

    /// <summary>
    /// Extracts the document category from a DocumentReference
    /// </summary>
    /// <param name="documentReference">The DocumentReference resource</param>
    /// <returns>The document category code or null if not found</returns>
    string? ExtractDocumentCategory(DocumentReferenceData documentReference);

    /// <summary>
    /// Extracts the document type from a DocumentReference
    /// </summary>
    /// <param name="documentReference">The DocumentReference resource</param>
    /// <returns>The document type formatted as "Display(Code)" or null if not found</returns>
    string? ExtractDocumentType(DocumentReferenceData documentReference);

    /// <summary>
    /// Extracts encounter reference from a DocumentReference
    /// </summary>
    /// <param name="documentReference">The DocumentReference resource</param>
    /// <returns>The encounter reference or null if not found</returns>
    string? ExtractEncounterReference(DocumentReferenceData documentReference);

    /// <summary>
    /// Validates and sanitizes tag values for S3 compatibility
    /// </summary>
    /// <param name="tagValue">The tag value to sanitize</param>
    /// <returns>S3-compatible tag value</returns>
    string SanitizeTagValue(string? tagValue);

    /// <summary>
    /// Validates and sanitizes tag values for S3 compatibility with custom max length
    /// </summary>
    /// <param name="tagValue">The tag value to sanitize</param>
    /// <param name="maxLength">Maximum length for the tag value (default 128)</param>
    /// <returns>S3-compatible tag value</returns>
    string SanitizeTagValue(string? tagValue, int maxLength);
}