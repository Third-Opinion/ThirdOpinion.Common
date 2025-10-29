using ThirdOpinion.Common.Fhir.Documents.Models;

namespace ThirdOpinion.Common.Fhir.Documents;

/// <summary>
/// Service interface for generating file names and organizing folder structure
/// </summary>
public interface IFileOrganizationService
{
    /// <summary>
    /// Generates a file name for a binary file with original filename
    /// Format: {patientId}-{DocumentRefId}-{ArrayIndex}-{FileName}.{type}
    /// </summary>
    /// <param name="patientId">Patient identifier</param>
    /// <param name="documentRefId">DocumentReference identifier</param>
    /// <param name="arrayIndex">Index of content attachment (0-based)</param>
    /// <param name="originalFileName">Original filename from attachment</param>
    /// <param name="contentType">MIME content type</param>
    /// <returns>Generated filename</returns>
    string GenerateBinaryFileName(
        string patientId,
        string documentRefId,
        int arrayIndex,
        string? originalFileName,
        string contentType);

    /// <summary>
    /// Generates a file name for embedded base64 content
    /// Format: {patientId}-{DocumentRefId}-{ArrayIndex}.{type}
    /// </summary>
    /// <param name="patientId">Patient identifier</param>
    /// <param name="documentRefId">DocumentReference identifier</param>
    /// <param name="arrayIndex">Index of content attachment (0-based)</param>
    /// <param name="contentType">MIME content type</param>
    /// <returns>Generated filename</returns>
    string GenerateEmbeddedFileName(
        string patientId,
        string documentRefId,
        int arrayIndex,
        string contentType);

    /// <summary>
    /// Generates the S3 key (full path) for a document including patient folder organization
    /// Format: {s3KeyPrefix}/{practiceName}_{practiceId}/{patientId}/{fileName}
    /// </summary>
    /// <param name="practiceInfo">Practice information for folder structure</param>
    /// <param name="patientId">Patient identifier for folder organization</param>
    /// <param name="fileName">Generated filename</param>
    /// <param name="s3KeyPrefix">Optional S3 key prefix to prepend to the key path</param>
    /// <returns>S3 key path</returns>
    string GenerateS3Key(PracticeInfo practiceInfo, string patientId, string fileName, string? s3KeyPrefix = null);

    /// <summary>
    /// Determines file extension from MIME content type
    /// </summary>
    /// <param name="contentType">MIME content type</param>
    /// <returns>File extension including dot (e.g., ".pdf", ".png")</returns>
    string GetFileExtensionFromContentType(string contentType);

    /// <summary>
    /// Sanitizes filename to ensure it's safe for file systems and S3
    /// </summary>
    /// <param name="fileName">Original filename</param>
    /// <returns>Sanitized filename</returns>
    string SanitizeFileName(string fileName);

    /// <summary>
    /// Validates that required components are present and valid
    /// </summary>
    /// <param name="patientId">Patient ID to validate</param>
    /// <param name="documentRefId">Document reference ID to validate</param>
    /// <param name="contentType">Content type to validate</param>
    /// <returns>True if all components are valid</returns>
    bool ValidateFileNameComponents(string patientId, string documentRefId, string contentType);
}