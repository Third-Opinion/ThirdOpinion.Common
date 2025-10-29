using System.Text;
using ThirdOpinion.Common.Fhir.Documents.Exceptions;
using ThirdOpinion.Common.Fhir.Documents.Models;
using ThirdOpinion.Common.Aws.HealthLake;
using ThirdOpinion.Common.Logging;
using Microsoft.Extensions.Logging;

namespace ThirdOpinion.Common.Fhir.Documents;

/// <summary>
/// Service for extracting and decoding base64-encoded content from DocumentReference attachments
/// </summary>
public class Base64ContentExtractor : IBase64ContentExtractor
{
    private readonly ILogger<Base64ContentExtractor> _logger;
    private readonly ICorrelationIdProvider _correlationIdProvider;

    // Common MIME type to file extension mappings
    private static readonly Dictionary<string, string> ContentTypeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Images
        { "image/jpeg", ".jpg" },
        { "image/jpg", ".jpg" },
        { "image/png", ".png" },
        { "image/gif", ".gif" },
        { "image/bmp", ".bmp" },
        { "image/tiff", ".tiff" },
        { "image/webp", ".webp" },
        { "image/svg+xml", ".svg" },

        // Documents
        { "application/pdf", ".pdf" },
        { "application/msword", ".doc" },
        { "application/vnd.openxmlformats-officedocument.wordprocessingml.document", ".docx" },
        { "application/vnd.ms-excel", ".xls" },
        { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ".xlsx" },
        { "application/vnd.ms-powerpoint", ".ppt" },
        { "application/vnd.openxmlformats-officedocument.presentationml.presentation", ".pptx" },
        { "application/rtf", ".rtf" },

        // Text formats
        { "text/plain", ".txt" },
        { "text/html", ".html" },
        { "text/css", ".css" },
        { "text/javascript", ".js" },
        { "text/xml", ".xml" },
        { "text/csv", ".csv" },

        // Archives
        { "application/zip", ".zip" },
        { "application/x-rar-compressed", ".rar" },
        { "application/x-7z-compressed", ".7z" },

        // Audio/Video
        { "audio/mpeg", ".mp3" },
        { "audio/wav", ".wav" },
        { "video/mp4", ".mp4" },
        { "video/avi", ".avi" },

        // Medical formats
        { "application/dicom", ".dcm" },
        { "text/hl7", ".hl7" },

        // Other common formats
        { "application/json", ".json" },
        { "application/xml", ".xml" },
        { "application/octet-stream", ".bin" }
    };

    public Base64ContentExtractor(
        ILogger<Base64ContentExtractor> logger,
        ICorrelationIdProvider correlationIdProvider)
    {
        _logger = logger;
        _correlationIdProvider = correlationIdProvider;
    }

    public async Task<DecodedContent> ExtractAndDecodeAsync(Attachment attachment, CancellationToken cancellationToken = default)
    {
        var correlationId = _correlationIdProvider.GetCorrelationId();

        if (attachment == null)
        {
            throw new DocumentDownloadException("Attachment cannot be null", false, ErrorCategory.BusinessLogic);
        }

        if (!HasEmbeddedContent(attachment))
        {
            throw new DocumentDownloadException(
                "Attachment does not contain embedded base64 data",
                false,
                ErrorCategory.BusinessLogic);
        }

        try
        {
            _logger.LogDebug("Extracting base64 content from attachment. ContentType: {ContentType}, Title: {Title} [CorrelationId: {CorrelationId}]",
                attachment.ContentType, attachment.Title, correlationId);

            // Decode the base64 data
            var decodedData = Convert.FromBase64String(attachment.Data!);

            _logger.LogDebug("Successfully decoded base64 content. Original size: {OriginalSize} chars, Decoded size: {DecodedSize} bytes [CorrelationId: {CorrelationId}]",
                attachment.Data!.Length, decodedData.Length, correlationId);

            // Determine file extension
            var fileExtension = GetFileExtensionFromContentType(attachment.ContentType);

            var result = new DecodedContent
            {
                Data = decodedData,
                ContentType = attachment.ContentType,
                FileExtension = fileExtension,
                Title = attachment.Title
            };

            _logger.LogInformation("Successfully extracted and decoded base64 content. Size: {SizeBytes} bytes, Extension: {Extension} [CorrelationId: {CorrelationId}]",
                result.SizeBytes, result.FileExtension, correlationId);

            return await Task.FromResult(result);
        }
        catch (FormatException ex)
        {
            _logger.LogError(ex, "Invalid base64 format in attachment data [CorrelationId: {CorrelationId}]", correlationId);
            throw new DocumentDownloadException("Invalid base64 format in attachment data", ex, false, ErrorCategory.BusinessLogic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error extracting base64 content [CorrelationId: {CorrelationId}]", correlationId);
            throw new DocumentDownloadException("Failed to extract base64 content", ex, true, ErrorCategory.Infrastructure);
        }
    }

    public string GetFileExtensionFromContentType(string? contentType)
    {
        var correlationId = _correlationIdProvider.GetCorrelationId();

        if (string.IsNullOrWhiteSpace(contentType))
        {
            _logger.LogWarning("No content type provided, using default .bin extension [CorrelationId: {CorrelationId}]", correlationId);
            return ".bin";
        }

        // Clean up the content type (remove charset and other parameters)
        var cleanContentType = contentType.Split(';')[0].Trim().ToLowerInvariant();

        if (ContentTypeExtensions.TryGetValue(cleanContentType, out var extension))
        {
            _logger.LogDebug("Mapped content type '{ContentType}' to extension '{Extension}' [CorrelationId: {CorrelationId}]",
                cleanContentType, extension, correlationId);
            return extension;
        }

        _logger.LogWarning("Unknown content type '{ContentType}', using default .bin extension [CorrelationId: {CorrelationId}]",
            cleanContentType, correlationId);
        return ".bin";
    }

    public bool HasEmbeddedContent(Attachment attachment)
    {
        return attachment?.IsEmbeddedContent == true && !string.IsNullOrEmpty(attachment.Data);
    }
}