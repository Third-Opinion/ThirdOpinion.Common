using System.Text.RegularExpressions;
using ThirdOpinion.Common.Fhir.Documents.Models;
using ThirdOpinion.Common.Logging;
using Microsoft.Extensions.Logging;

namespace ThirdOpinion.Common.Fhir.Documents;

/// <summary>
/// Service for generating file names and organizing folder structure for document storage
/// </summary>
public class FileOrganizationService : IFileOrganizationService
{
    private readonly ILogger<FileOrganizationService> _logger;
    private readonly ICorrelationIdProvider _correlationIdProvider;

    // Common MIME type to extension mappings
    private static readonly Dictionary<string, string> ContentTypeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        { "application/pdf", ".pdf" },
        { "image/png", ".png" },
        { "image/jpeg", ".jpg" },
        { "image/jpg", ".jpg" },
        { "image/gif", ".gif" },
        { "image/tiff", ".tiff" },
        { "image/tif", ".tif" },
        { "text/html", ".html" },
        { "text/plain", ".txt" },
        { "text/xml", ".xml" },
        { "application/xml", ".xml" },
        { "application/json", ".json" },
        { "application/fhir+json", ".json" },
        { "application/msword", ".doc" },
        { "application/vnd.openxmlformats-officedocument.wordprocessingml.document", ".docx" },
        { "application/vnd.ms-excel", ".xls" },
        { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ".xlsx" },
        { "application/octet-stream", ".bin" }
    };

    // Regex for sanitizing filenames
    private static readonly Regex InvalidFileNameChars = new(@"[<>:""/\\|?*\x00-\x1f]", RegexOptions.Compiled);
    private static readonly Regex MultipleSpaces = new(@"\s+", RegexOptions.Compiled);

    public FileOrganizationService(
        ILogger<FileOrganizationService> logger,
        ICorrelationIdProvider correlationIdProvider)
    {
        _logger = logger;
        _correlationIdProvider = correlationIdProvider;
    }

    public string GenerateBinaryFileName(
        string patientId,
        string documentRefId,
        int arrayIndex,
        string? originalFileName,
        string contentType)
    {
        var correlationId = _correlationIdProvider.GetCorrelationId();

        if (!ValidateFileNameComponents(patientId, documentRefId, contentType))
        {
            throw new ArgumentException("Invalid file name components provided");
        }

        try
        {
            var extension = GetFileExtensionFromContentType(contentType);

            // Clean up the original filename if provided
            string baseFileName;
            if (!string.IsNullOrWhiteSpace(originalFileName))
            {
                // Remove extension from original filename and sanitize
                var sanitizedOriginal = SanitizeFileName(Path.GetFileNameWithoutExtension(originalFileName));
                baseFileName = $"{documentRefId}-{arrayIndex}-{sanitizedOriginal}{extension}";
            }
            else
            {
                // No original filename provided, use content type info
                var typeHint = GetTypeHintFromContentType(contentType);
                baseFileName = $"{documentRefId}-{arrayIndex}-{typeHint}{extension}";
            }

            _logger.LogDebug("Generated binary filename: {FileName} for patient: {PatientId}, document: {DocumentId}, index: {Index} [CorrelationId: {CorrelationId}]",
                baseFileName, patientId, documentRefId, arrayIndex, correlationId);

            return baseFileName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating binary filename for patient: {PatientId}, document: {DocumentId} [CorrelationId: {CorrelationId}]",
                patientId, documentRefId, correlationId);
            throw;
        }
    }

    public string GenerateEmbeddedFileName(
        string patientId,
        string documentRefId,
        int arrayIndex,
        string contentType)
    {
        var correlationId = _correlationIdProvider.GetCorrelationId();

        if (!ValidateFileNameComponents(patientId, documentRefId, contentType))
        {
            throw new ArgumentException("Invalid file name components provided");
        }

        try
        {
            var extension = GetFileExtensionFromContentType(contentType);
            var fileName = $"{documentRefId}-{arrayIndex}{extension}";

            _logger.LogDebug("Generated embedded filename: {FileName} for patient: {PatientId}, document: {DocumentId}, index: {Index} [CorrelationId: {CorrelationId}]",
                fileName, patientId, documentRefId, arrayIndex, correlationId);

            return fileName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embedded filename for patient: {PatientId}, document: {DocumentId} [CorrelationId: {CorrelationId}]",
                patientId, documentRefId, correlationId);
            throw;
        }
    }

    public string GenerateS3Key(PracticeInfo practiceInfo, string patientId, string fileName, string? s3KeyPrefix = null)
    {
        var correlationId = _correlationIdProvider.GetCorrelationId();

        if (practiceInfo == null)
        {
            throw new ArgumentNullException(nameof(practiceInfo));
        }

        if (string.IsNullOrWhiteSpace(patientId))
        {
            throw new ArgumentException("Patient ID cannot be null or empty", nameof(patientId));
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name cannot be null or empty", nameof(fileName));
        }

        try
        {
            var folderName = practiceInfo.GetFolderName();
            var sanitizedPatientId = SanitizeFileName(patientId);

            // Build S3 key with optional prefix: {prefix}/{practice}/{patientId}/{fileName}
            var s3Key = string.IsNullOrEmpty(s3KeyPrefix)
                ? $"{folderName}/{sanitizedPatientId}/{fileName}"
                : $"{s3KeyPrefix.TrimEnd('/')}/{folderName}/{sanitizedPatientId}/{fileName}";

            _logger.LogDebug("Generated S3 key: {S3Key} for practice: {PracticeName}_{PracticeId}, patient: {PatientId}, file: {FileName}, prefix: {S3KeyPrefix} [CorrelationId: {CorrelationId}]",
                s3Key, practiceInfo.Name, practiceInfo.Id, patientId, fileName, s3KeyPrefix ?? "none", correlationId);

            return s3Key;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating S3 key for practice: {PracticeName}_{PracticeId}, patient: {PatientId}, file: {FileName} [CorrelationId: {CorrelationId}]",
                practiceInfo.Name, practiceInfo.Id, patientId, fileName, correlationId);
            throw;
        }
    }

    public string GetFileExtensionFromContentType(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return ".bin"; // Default extension for unknown types
        }

        // Remove any charset or other parameters from content type
        var cleanContentType = contentType.Split(';')[0].Trim().ToLowerInvariant();

        if (ContentTypeExtensions.TryGetValue(cleanContentType, out var extension))
        {
            return extension;
        }

        // Try to derive extension from content type
        if (cleanContentType.StartsWith("image/"))
        {
            var subtype = cleanContentType.Substring(6);
            return $".{subtype}";
        }

        if (cleanContentType.StartsWith("text/"))
        {
            var subtype = cleanContentType.Substring(5);
            return subtype switch
            {
                "plain" => ".txt",
                "html" => ".html",
                "css" => ".css",
                "javascript" => ".js",
                _ => ".txt"
            };
        }

        _logger.LogWarning("Unknown content type: {ContentType}, using .bin extension", contentType);
        return ".bin";
    }

    public string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "unknown";
        }

        // Remove invalid characters
        var sanitized = InvalidFileNameChars.Replace(fileName, "_");

        // Replace multiple spaces with single space
        sanitized = MultipleSpaces.Replace(sanitized, " ");

        // Trim and limit length
        sanitized = sanitized.Trim().Substring(0, Math.Min(sanitized.Length, 100));

        // Ensure we don't end up with empty string
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "unknown";
        }

        return sanitized;
    }

    public bool ValidateFileNameComponents(string patientId, string documentRefId, string contentType)
    {
        if (string.IsNullOrWhiteSpace(patientId))
        {
            _logger.LogWarning("Patient ID is null or empty");
            return false;
        }

        if (string.IsNullOrWhiteSpace(documentRefId))
        {
            _logger.LogWarning("Document reference ID is null or empty");
            return false;
        }

        if (string.IsNullOrWhiteSpace(contentType))
        {
            _logger.LogWarning("Content type is null or empty");
            return false;
        }

        // Validate patient ID format (should not contain invalid characters)
        if (patientId.Any(c => Path.GetInvalidFileNameChars().Contains(c)))
        {
            _logger.LogWarning("Patient ID contains invalid characters: {PatientId}", patientId);
            return false;
        }

        // Validate document reference ID format
        if (documentRefId.Any(c => Path.GetInvalidFileNameChars().Contains(c)))
        {
            _logger.LogWarning("Document reference ID contains invalid characters: {DocumentRefId}", documentRefId);
            return false;
        }

        return true;
    }

    private string GetTypeHintFromContentType(string contentType)
    {
        var cleanContentType = contentType.Split(';')[0].Trim().ToLowerInvariant();

        return cleanContentType switch
        {
            var ct when ct.StartsWith("image/") => "image",
            var ct when ct.StartsWith("text/") => "text",
            "application/pdf" => "pdf",
            "application/msword" => "document",
            var ct when ct.Contains("word") => "document",
            var ct when ct.Contains("excel") => "spreadsheet",
            _ => "file"
        };
    }
}