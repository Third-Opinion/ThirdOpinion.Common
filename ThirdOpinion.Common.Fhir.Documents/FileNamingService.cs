using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace ThirdOpinion.Common.Fhir.Documents;

/// <summary>
///     Types of prompts that determine output file extensions
/// </summary>
public enum PromptType
{
    /// <summary>
    ///     Default markdown output
    /// </summary>
    Markdown,

    /// <summary>
    ///     Fact extraction output in JSON format
    /// </summary>
    FactExtraction,

    /// <summary>
    ///     Cancer analysis output in JSON format
    /// </summary>
    CancerAnalysis,

    /// <summary>
    ///     Generic JSON output
    /// </summary>
    Json
}

/// <summary>
///     Service for generating standardized output filenames for document processing
/// </summary>
public class FileNamingService : IFileNamingService
{
    private static readonly Regex InvalidFileNameCharsRegex
        = new(@"[<>:""/\\|?*\x00-\x1f]", RegexOptions.Compiled);

    private static readonly Regex MultipleUnderscoresRegex = new(@"_{2,}", RegexOptions.Compiled);
    private readonly ILogger<FileNamingService> _logger;

    public FileNamingService(ILogger<FileNamingService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string GenerateOutputFileName(string originalName,
        string version,
        string runId,
        PromptType promptType)
    {
        if (string.IsNullOrWhiteSpace(originalName))
            throw new ArgumentException("Original name cannot be null or whitespace",
                nameof(originalName));
        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("Version cannot be null or whitespace", nameof(version));
        if (string.IsNullOrWhiteSpace(runId))
            throw new ArgumentException("Run ID cannot be null or whitespace", nameof(runId));

        // Extract base name without extension
        string baseName = Path.GetFileNameWithoutExtension(originalName);

        // Sanitize the base name
        string sanitizedBaseName = SanitizeFileName(baseName);

        // Sanitize version and runId
        string sanitizedVersion = SanitizeFileName(version);
        string sanitizedRunId = SanitizeFileName(runId);

        // Get appropriate extension for prompt type
        string extension = GetExtensionForPromptType(promptType);

        // Generate filename: originalName_v{version}_{runId}.{extension}
        var fileName = $"{sanitizedBaseName}_v{sanitizedVersion}_{sanitizedRunId}{extension}";

        // Ensure filename doesn't exceed maximum length (keeping some buffer for S3 key paths)
        const int maxFileNameLength = 200;
        if (fileName.Length > maxFileNameLength)
        {
            // Truncate the base name to fit within limits
            int availableLength = maxFileNameLength -
                                  $"_v{sanitizedVersion}_{sanitizedRunId}{extension}".Length;
            if (availableLength > 0)
            {
                sanitizedBaseName
                    = sanitizedBaseName[..Math.Min(sanitizedBaseName.Length, availableLength)];
                fileName = $"{sanitizedBaseName}_v{sanitizedVersion}_{sanitizedRunId}{extension}";
            }
            else
            {
                throw new ArgumentException(
                    $"Version and run ID are too long to generate a valid filename. Combined length exceeds {maxFileNameLength} characters.");
            }
        }

        _logger.LogDebug(
            "Generated filename: {FileName} from original: {OriginalName}, version: {Version}, runId: {RunId}, promptType: {PromptType}",
            fileName, originalName, version, runId, promptType);

        return fileName;
    }

    /// <inheritdoc />
    public string GenerateOutputFileName(string originalName,
        int version,
        string runId,
        PromptType promptType)
    {
        return GenerateOutputFileName(originalName, version.ToString(), runId, promptType);
    }

    /// <inheritdoc />
    public string GetExtensionForPromptType(PromptType promptType)
    {
        return promptType switch
        {
            PromptType.Markdown => ".md",
            PromptType.FactExtraction => ".fact.json",
            PromptType.CancerAnalysis => ".cancer.json",
            PromptType.Json => ".json",
            _ => throw new ArgumentOutOfRangeException(nameof(promptType), promptType,
                "Unknown prompt type")
        };
    }

    /// <inheritdoc />
    public PromptType DeterminePromptTypeFromName(string promptName)
    {
        if (string.IsNullOrWhiteSpace(promptName))
            return PromptType.Json; // Default fallback

        string lowerPromptName = promptName.ToLowerInvariant();

        return lowerPromptName switch
        {
            _ when lowerPromptName.Contains("fact") => PromptType.FactExtraction,
            _ when lowerPromptName.Contains("cancer") => PromptType.CancerAnalysis,
            _ when lowerPromptName.Contains("markdown") || lowerPromptName.Contains("md") =>
                PromptType.Markdown,
            _ => PromptType.Json // Default fallback
        };
    }

    /// <inheritdoc />
    public string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "unnamed";

        // Remove invalid characters
        string sanitized = InvalidFileNameCharsRegex.Replace(fileName, "_");

        // Replace multiple consecutive underscores with single underscore
        sanitized = MultipleUnderscoresRegex.Replace(sanitized, "_");

        // Remove leading/trailing underscores and whitespace
        sanitized = sanitized.Trim('_', ' ', '\t');

        // Ensure we have at least something
        if (string.IsNullOrEmpty(sanitized))
            sanitized = "unnamed";

        return sanitized;
    }

    /// <inheritdoc />
    public bool IsValidFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        // Check for invalid characters
        if (InvalidFileNameCharsRegex.IsMatch(fileName))
            return false;

        // Check for reserved names on Windows
        var reservedNames = new[]
        {
            "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7",
            "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };
        string baseNameUpper = Path.GetFileNameWithoutExtension(fileName).ToUpperInvariant();

        if (reservedNames.Contains(baseNameUpper))
            return false;

        // Check length
        return fileName.Length <= 255;
    }

    /// <inheritdoc />
    public string GenerateVersionedFileName(string baseName, int version, string? suffix = null)
    {
        string sanitizedBaseName = SanitizeFileName(baseName);
        var versionPart = $"_v{version:D3}";
        string suffixPart = !string.IsNullOrEmpty(suffix) ? $"_{SanitizeFileName(suffix)}" : "";

        return $"{sanitizedBaseName}{versionPart}{suffixPart}";
    }

    /// <inheritdoc />
    public (string baseName, int version, string? suffix) ParseVersionedFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Filename cannot be null or whitespace", nameof(fileName));

        string nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);

        // Look for version pattern: _v{number}
        Match versionMatch = Regex.Match(nameWithoutExtension, @"_v(\d+)(?:_(.+))?$");

        if (!versionMatch.Success)
            // No version found, return original name with version 1
            return (nameWithoutExtension, 1, null);

        int version = int.Parse(versionMatch.Groups[1].Value);
        string? suffix = versionMatch.Groups[2].Success ? versionMatch.Groups[2].Value : null;
        string baseName = nameWithoutExtension[..versionMatch.Index];

        return (baseName, version, suffix);
    }
}