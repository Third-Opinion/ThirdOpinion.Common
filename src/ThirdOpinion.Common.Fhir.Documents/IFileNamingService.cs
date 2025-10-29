namespace ThirdOpinion.Common.Fhir.Documents;

/// <summary>
/// Service for generating standardized output filenames for document processing
/// </summary>
public interface IFileNamingService
{
    /// <summary>
    /// Generates an output filename following the pattern: originalName_v{version}_{runId}.{extension}
    /// </summary>
    /// <param name="originalName">Original document filename (with or without extension)</param>
    /// <param name="version">Version string (e.g., "1", "2", "001")</param>
    /// <param name="runId">Unique run identifier</param>
    /// <param name="promptType">Type of prompt determining the output extension</param>
    /// <returns>Generated filename with appropriate extension</returns>
    /// <exception cref="ArgumentException">Thrown when required parameters are null or whitespace</exception>
    string GenerateOutputFileName(string originalName, string version, string runId, PromptType promptType);

    /// <summary>
    /// Generates an output filename following the pattern: originalName_v{version}_{runId}.{extension}
    /// </summary>
    /// <param name="originalName">Original document filename (with or without extension)</param>
    /// <param name="version">Version number</param>
    /// <param name="runId">Unique run identifier</param>
    /// <param name="promptType">Type of prompt determining the output extension</param>
    /// <returns>Generated filename with appropriate extension</returns>
    /// <exception cref="ArgumentException">Thrown when required parameters are null or whitespace</exception>
    string GenerateOutputFileName(string originalName, int version, string runId, PromptType promptType);

    /// <summary>
    /// Gets the appropriate file extension for a given prompt type
    /// </summary>
    /// <param name="promptType">Type of prompt</param>
    /// <returns>File extension including the dot (e.g., ".json", ".md")</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for unknown prompt types</exception>
    string GetExtensionForPromptType(PromptType promptType);

    /// <summary>
    /// Determines the prompt type based on the prompt name
    /// </summary>
    /// <param name="promptName">Name of the prompt</param>
    /// <returns>Determined prompt type, defaults to Json if cannot be determined</returns>
    PromptType DeterminePromptTypeFromName(string promptName);

    /// <summary>
    /// Sanitizes a filename by removing invalid characters and ensuring compliance with file system constraints
    /// </summary>
    /// <param name="fileName">Filename to sanitize</param>
    /// <returns>Sanitized filename safe for file system use</returns>
    string SanitizeFileName(string fileName);

    /// <summary>
    /// Validates if a filename is valid for file system use
    /// </summary>
    /// <param name="fileName">Filename to validate</param>
    /// <returns>True if the filename is valid, false otherwise</returns>
    bool IsValidFileName(string fileName);

    /// <summary>
    /// Generates a versioned filename with optional suffix
    /// </summary>
    /// <param name="baseName">Base filename</param>
    /// <param name="version">Version number</param>
    /// <param name="suffix">Optional suffix to append</param>
    /// <returns>Versioned filename</returns>
    string GenerateVersionedFileName(string baseName, int version, string? suffix = null);

    /// <summary>
    /// Parses a versioned filename to extract base name, version, and suffix
    /// </summary>
    /// <param name="fileName">Filename to parse</param>
    /// <returns>Tuple containing base name, version number, and optional suffix</returns>
    /// <exception cref="ArgumentException">Thrown when filename is null or whitespace</exception>
    (string baseName, int version, string? suffix) ParseVersionedFileName(string fileName);
}