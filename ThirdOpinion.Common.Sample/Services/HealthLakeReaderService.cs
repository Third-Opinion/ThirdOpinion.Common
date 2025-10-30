using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThirdOpinion.Common.Aws.HealthLake.Configuration;
using ThirdOpinion.Common.Aws.HealthLake.Http;
using ThirdOpinion.Common.Logging;

namespace ThirdOpinion.Common.Sample.Services;

/// <summary>
/// Service for reading FHIR resources from AWS HealthLake
/// </summary>
public class HealthLakeReaderService
{
    private readonly HealthLakeConfig _config;
    private readonly IHealthLakeHttpService _healthLakeHttpService;
    private readonly ILogger<HealthLakeReaderService> _logger;
    private readonly ICorrelationIdProvider _correlationIdProvider;

    public HealthLakeReaderService(
        IOptions<HealthLakeConfig> config,
        IHealthLakeHttpService healthLakeHttpService,
        ILogger<HealthLakeReaderService> logger,
        ICorrelationIdProvider correlationIdProvider)
    {
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _healthLakeHttpService = healthLakeHttpService ?? throw new ArgumentNullException(nameof(healthLakeHttpService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _correlationIdProvider = correlationIdProvider ?? throw new ArgumentNullException(nameof(correlationIdProvider));
    }

    /// <summary>
    /// Retrieves a FHIR DocumentReference resource and extracts base64 document content
    /// </summary>
    /// <param name="documentReferenceId">The ID of the DocumentReference resource</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Document content as bytes and MIME type</returns>
    public async Task<DocumentContent?> GetDocumentReferenceContentAsync(string documentReferenceId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving DocumentReference {DocumentReferenceId} from HealthLake", documentReferenceId);

        try
        {
            // Build the FHIR REST API URL
            var endpoint = $"healthlake.{_config.Region}.amazonaws.com";
            var url = $"https://{endpoint}/datastore/{_config.DatastoreId}/r4/DocumentReference/{documentReferenceId}";

            // Create HTTP GET request
            var request = new HttpRequestMessage(HttpMethod.Get, url);

            // Add required headers
            request.Headers.Add("Accept", "application/fhir+json");
            request.Headers.Add("X-Correlation-ID", _correlationIdProvider.GetCorrelationId());

            _logger.LogDebug("Sending GET request to HealthLake: {Url}", url);

            // Send signed request to HealthLake
            var response = await _healthLakeHttpService.SendSignedRequestAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to retrieve DocumentReference {DocumentReferenceId}. Status: {StatusCode}, Content: {ErrorContent}",
                    documentReferenceId, response.StatusCode, errorContent);
                return null;
            }

            // Parse the FHIR DocumentReference response
            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogDebug("Received DocumentReference JSON: {JsonLength} characters", jsonContent.Length);

            var documentReference = JsonSerializer.Deserialize<JsonElement>(jsonContent);

            // Extract document content from the DocumentReference
            return ExtractDocumentContent(documentReference, documentReferenceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving DocumentReference {DocumentReferenceId}", documentReferenceId);
            throw;
        }
    }

    /// <summary>
    /// Extracts base64 document content from a FHIR DocumentReference JSON
    /// </summary>
    private DocumentContent? ExtractDocumentContent(JsonElement documentReference, string documentReferenceId)
    {
        _logger.LogDebug("Extracting document content from DocumentReference {DocumentReferenceId}", documentReferenceId);

        try
        {
            // Navigate to content array
            if (!documentReference.TryGetProperty("content", out var contentArray) || contentArray.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning("DocumentReference {DocumentReferenceId} has no content array", documentReferenceId);
                return null;
            }

            // Get the first content element
            if (contentArray.GetArrayLength() == 0)
            {
                _logger.LogWarning("DocumentReference {DocumentReferenceId} has empty content array", documentReferenceId);
                return null;
            }

            var firstContent = contentArray[0];

            // Navigate to attachment
            if (!firstContent.TryGetProperty("attachment", out var attachment))
            {
                _logger.LogWarning("DocumentReference {DocumentReferenceId} content has no attachment", documentReferenceId);
                return null;
            }

            // Extract base64 data
            if (!attachment.TryGetProperty("data", out var dataElement))
            {
                _logger.LogWarning("DocumentReference {DocumentReferenceId} attachment has no data field", documentReferenceId);
                return null;
            }

            var base64Data = dataElement.GetString();
            if (string.IsNullOrEmpty(base64Data))
            {
                _logger.LogWarning("DocumentReference {DocumentReferenceId} has empty base64 data", documentReferenceId);
                return null;
            }

            // Extract MIME type (optional)
            string? mimeType = null;
            if (attachment.TryGetProperty("contentType", out var contentTypeElement))
            {
                mimeType = contentTypeElement.GetString();
            }

            // Extract filename (optional)
            string? filename = null;
            if (attachment.TryGetProperty("title", out var titleElement))
            {
                filename = titleElement.GetString();
            }

            _logger.LogInformation("Successfully extracted document content from DocumentReference {DocumentReferenceId}. " +
                                 "MIME Type: {MimeType}, Filename: {Filename}, Base64 Length: {Base64Length}",
                                 documentReferenceId, mimeType ?? "unknown", filename ?? "unknown", base64Data.Length);

            // Decode base64 data
            byte[] documentBytes;
            try
            {
                documentBytes = Convert.FromBase64String(base64Data);
            }
            catch (FormatException ex)
            {
                _logger.LogError(ex, "Failed to decode base64 data from DocumentReference {DocumentReferenceId}", documentReferenceId);
                return null;
            }

            return new DocumentContent
            {
                Data = documentBytes,
                MimeType = mimeType,
                Filename = filename
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting content from DocumentReference {DocumentReferenceId}", documentReferenceId);
            return null;
        }
    }
}

/// <summary>
/// Represents the content of a document extracted from a FHIR DocumentReference
/// </summary>
public class DocumentContent
{
    /// <summary>
    /// The document data as bytes
    /// </summary>
    public byte[] Data { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// The MIME type of the document (e.g., "application/pdf", "image/jpeg")
    /// </summary>
    public string? MimeType { get; set; }

    /// <summary>
    /// The original filename of the document (if available)
    /// </summary>
    public string? Filename { get; set; }

    /// <summary>
    /// Gets the suggested file extension based on MIME type
    /// </summary>
    public string GetFileExtension()
    {
        return MimeType?.ToLowerInvariant() switch
        {
            "application/pdf" => ".pdf",
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/tiff" => ".tiff",
            "text/plain" => ".txt",
            "application/msword" => ".doc",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
            _ => ".bin" // Binary file as fallback
        };
    }
}