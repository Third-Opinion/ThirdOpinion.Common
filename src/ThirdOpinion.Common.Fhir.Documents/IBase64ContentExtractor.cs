using ThirdOpinion.Common.Fhir.Documents.Models;

namespace ThirdOpinion.Common.Fhir.Documents;

/// <summary>
/// Service for extracting and decoding base64-encoded content from DocumentReference attachments
/// </summary>
public interface IBase64ContentExtractor
{
    /// <summary>
    /// Extracts and decodes base64 content from a DocumentReference attachment
    /// </summary>
    /// <param name="attachment">The attachment containing base64 data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The decoded content data and metadata</returns>
    Task<DecodedContent> ExtractAndDecodeAsync(Attachment attachment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines the file extension from the content type
    /// </summary>
    /// <param name="contentType">The MIME content type</param>
    /// <returns>The appropriate file extension including the dot</returns>
    string GetFileExtensionFromContentType(string? contentType);

    /// <summary>
    /// Validates that the attachment contains embedded base64 data
    /// </summary>
    /// <param name="attachment">The attachment to validate</param>
    /// <returns>True if the attachment contains embedded data</returns>
    bool HasEmbeddedContent(Attachment attachment);
}

/// <summary>
/// Represents decoded content from a base64-encoded attachment
/// </summary>
public class DecodedContent
{
    /// <summary>
    /// The decoded binary data
    /// </summary>
    public required byte[] Data { get; set; }

    /// <summary>
    /// The content type of the decoded data
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// The file extension derived from the content type
    /// </summary>
    public required string FileExtension { get; set; }

    /// <summary>
    /// The size of the decoded data in bytes
    /// </summary>
    public long SizeBytes => Data.Length;

    /// <summary>
    /// The title from the attachment, if available
    /// </summary>
    public string? Title { get; set; }
}