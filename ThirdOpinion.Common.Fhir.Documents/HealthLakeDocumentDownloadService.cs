using System.Net;
using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThirdOpinion.Common.Aws.HealthLake.Configuration;
using ThirdOpinion.Common.Aws.HealthLake.Http;
using ThirdOpinion.Common.Fhir.Documents.Exceptions;
using ThirdOpinion.Common.Fhir.Documents.Models;
using ThirdOpinion.Common.Logging;

namespace ThirdOpinion.Common.Fhir.Documents;

/// <summary>
///     Main orchestration service for downloading DocumentReference content from HealthLake to S3
/// </summary>
public class HealthLakeDocumentDownloadService
{
    private readonly IBase64ContentExtractor _base64ContentExtractor;
    private readonly IBinaryDownloadService _binaryDownloadService;
    private readonly IBundleParserService _bundleParserService;
    private readonly ICorrelationIdProvider _correlationIdProvider;
    private readonly IFileOrganizationService _fileOrganizationService;
    private readonly HealthLakeConfig _healthLakeConfig;
    private readonly IHealthLakeHttpService _healthLakeHttpService;
    private readonly ILogger<HealthLakeDocumentDownloadService> _logger;
    private readonly IMetadataExtractorService _metadataExtractorService;
    private readonly IPatientEverythingService _patientEverythingService;
    private readonly string _s3BucketName;
    private readonly IAmazonS3 _s3Client;

    public HealthLakeDocumentDownloadService(
        IPatientEverythingService patientEverythingService,
        IBundleParserService bundleParserService,
        IFileOrganizationService fileOrganizationService,
        IBase64ContentExtractor base64ContentExtractor,
        IBinaryDownloadService binaryDownloadService,
        IMetadataExtractorService metadataExtractorService,
        ILogger<HealthLakeDocumentDownloadService> logger,
        ICorrelationIdProvider correlationIdProvider,
        IAmazonS3 s3Client,
        IHealthLakeHttpService healthLakeHttpService,
        IOptions<HealthLakeConfig> healthLakeConfig)
    {
        _patientEverythingService = patientEverythingService;
        _bundleParserService = bundleParserService;
        _fileOrganizationService = fileOrganizationService;
        _base64ContentExtractor = base64ContentExtractor;
        _binaryDownloadService = binaryDownloadService;
        _metadataExtractorService = metadataExtractorService;
        _logger = logger;
        _correlationIdProvider = correlationIdProvider;
        _s3Client = s3Client;
        _s3BucketName = "healthlake-documents"; // TODO: Add S3BucketName to HealthLakeConfig
        _healthLakeHttpService = healthLakeHttpService;
        _healthLakeConfig = healthLakeConfig.Value;
    }

    /// <summary>
    ///     Downloads all DocumentReferences for a patient and their content to S3
    /// </summary>
    /// <param name="patientId">The Patient ID to get DocumentReferences for</param>
    /// <param name="overridePracticeId">Optional practice ID override</param>
    /// <param name="s3KeyPrefix">Optional S3 key prefix override</param>
    /// <param name="specificDocumentReferenceId">
    ///     Optional specific DocumentReference ID to download (if provided, only this
    ///     one will be downloaded)
    /// </param>
    /// <param name="force">If true, skips user confirmation prompts</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Download results for all DocumentReferences found</returns>
    public async Task<List<DocumentDownloadResults>> DownloadPatientDocumentReferencesAsync(
        string patientId,
        string? overridePracticeId = null,
        string? s3KeyPrefix = null,
        string? s3Bucket = null,
        string? specificDocumentReferenceId = null,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        string correlationId = _correlationIdProvider.GetCorrelationId();

        _logger.LogInformation(
            "Starting Patient DocumentReference download: {PatientId} [CorrelationId: {CorrelationId}]",
            patientId, correlationId);

        var results = new List<DocumentDownloadResults>();

        try
        {
            // PHASE 1: Collect all DocumentReferences from all pages
            _logger.LogInformation(
                "===== PHASE 1: COLLECTING DOCUMENTREFERENCES ===== [CorrelationId: {CorrelationId}]",
                correlationId);
            _logger.LogInformation(
                "Starting DocumentReference collection for Patient {PatientId} [CorrelationId: {CorrelationId}]",
                patientId, correlationId);

            var allDocumentReferences = new List<DocumentReferenceData>();
            var pageNumber = 1;
            string? nextPageUrl = null;
            int? bundleTotal = null;

            // Collect all DocumentReferences first
            do
            {
                _logger.LogInformation(
                    "Retrieving page {PageNumber} of DocumentReferences for Patient {PatientId} [CorrelationId: {CorrelationId}]",
                    pageNumber, patientId, correlationId);

                // Get one page of DocumentReferences
                BundleData bundle
                    = await _patientEverythingService.GetPatientEverythingBundleAsync(patientId,
                        nextPageUrl, cancellationToken);

                if (!bundle.IsValidSearchsetBundle())
                    throw new DocumentDownloadException(
                        $"Invalid Bundle response: expected searchset Bundle, got {bundle.Type}",
                        false, ErrorCategory.BusinessLogic);

                // Log bundle total on first page
                if (pageNumber == 1 && bundle.Total.HasValue)
                {
                    bundleTotal = bundle.Total.Value;
                    _logger.LogInformation(
                        "Bundle reports total of {BundleTotal} resources available for patient: {PatientId} [CorrelationId: {CorrelationId}]",
                        bundleTotal.Value, patientId, correlationId);
                }

                List<DocumentReferenceData> documentReferences = bundle.GetDocumentReferences();
                allDocumentReferences.AddRange(documentReferences);

                _logger.LogInformation(
                    "Page {PageNumber}: Found {PageCount} DocumentReference(s), Total collected so far: {TotalCount} [CorrelationId: {CorrelationId}]",
                    pageNumber, documentReferences.Count, allDocumentReferences.Count,
                    correlationId);

                // Get the next page URL
                nextPageUrl = bundle.GetNextPageUrl();
                if (!string.IsNullOrEmpty(nextPageUrl))
                    _logger.LogDebug(
                        "Next page URL found, continuing to collect: {NextPageUrl} [CorrelationId: {CorrelationId}]",
                        nextPageUrl, correlationId);

                pageNumber++;
            } while (!string.IsNullOrEmpty(nextPageUrl));

            if (!allDocumentReferences.Any())
            {
                _logger.LogWarning(
                    "No DocumentReferences found for Patient {PatientId} [CorrelationId: {CorrelationId}]",
                    patientId, correlationId);
                return results;
            }

            // Filter to specific DocumentReference if requested
            if (!string.IsNullOrEmpty(specificDocumentReferenceId))
            {
                allDocumentReferences = allDocumentReferences
                    .Where(dr => dr.Id == specificDocumentReferenceId).ToList();
                if (!allDocumentReferences.Any())
                {
                    _logger.LogWarning(
                        "Specific DocumentReference {DocumentReferenceId} not found for Patient {PatientId} [CorrelationId: {CorrelationId}]",
                        specificDocumentReferenceId, patientId, correlationId);
                    return results;
                }
            }

            // Count total documents across all DocumentReferences
            var totalDocuments = 0;
            foreach (DocumentReferenceData docRef in allDocumentReferences)
                totalDocuments += docRef.Content?.Count ?? 0;

            // Display summary and list of DocumentReferences
            Console.WriteLine();
            Console.WriteLine("========================================");
            Console.WriteLine("DOCUMENTREFERENCE COLLECTION COMPLETE");
            Console.WriteLine("========================================");
            Console.WriteLine($"Patient ID: {patientId}");
            Console.WriteLine($"Total DocumentReferences found: {allDocumentReferences.Count}");
            Console.WriteLine($"Total documents in all references: {totalDocuments}");
            if (bundleTotal.HasValue)
                Console.WriteLine($"Bundle reported total: {bundleTotal.Value}");

            Console.WriteLine($"Pages retrieved: {pageNumber - 1}");
            Console.WriteLine();
            Console.WriteLine("DocumentReference List:");
            Console.WriteLine("----------------------------------------");

            // List each DocumentReference with ID and content type
            foreach (DocumentReferenceData docRef in allDocumentReferences)
            {
                var contentTypes = new List<string>();
                foreach (DocumentContent content in docRef.Content)
                    if (content.Attachment != null)
                    {
                        string contentType = content.Attachment.ContentType ?? "unknown";
                        contentTypes.Add(contentType);
                    }

                int contentCount = docRef.Content?.Count ?? 0;
                string contentTypeStr
                    = contentTypes.Any() ? string.Join(", ", contentTypes) : "no-content";

                // Show count if more than 1 document in this reference
                if (contentCount > 1)
                    Console.WriteLine(
                        $"  {docRef.Id} | {contentCount} documents | {contentTypeStr}");
                else
                    Console.WriteLine($"  {docRef.Id} | {contentTypeStr}");
            }

            Console.WriteLine("========================================");
            Console.WriteLine();

            // Prompt user to continue (unless force flag is set)
            if (!force)
            {
                Console.Write(
                    "Do you want to proceed with downloading these documents to S3? (yes/no): ");
                string? userInput = Console.ReadLine()?.Trim().ToLowerInvariant();

                if (userInput != "yes" && userInput != "y")
                {
                    _logger.LogInformation(
                        "User chose not to proceed with downloads for Patient {PatientId} [CorrelationId: {CorrelationId}]",
                        patientId, correlationId);
                    Console.WriteLine("Download cancelled by user.");
                    return results;
                }
            }
            else
            {
                _logger.LogInformation(
                    "Force flag enabled, skipping download confirmation prompt for Patient {PatientId} [CorrelationId: {CorrelationId}]",
                    patientId, correlationId);
            }

            // PHASE 2: Download documents to S3
            Console.WriteLine();
            _logger.LogInformation(
                "===== PHASE 2: DOWNLOADING TO S3 ===== [CorrelationId: {CorrelationId}]",
                correlationId);
            _logger.LogInformation(
                "User confirmed download. Starting S3 uploads for {Count} DocumentReference(s) [CorrelationId: {CorrelationId}]",
                allDocumentReferences.Count, correlationId);

            DateTime downloadStartTime = DateTime.UtcNow;
            var totalFilesProcessed = 0;
            var totalFilesSuccessful = 0;
            var totalBytesDownloaded = 0L;

            // Process each DocumentReference
            for (var i = 0; i < allDocumentReferences.Count; i++)
            {
                DocumentReferenceData documentReference = allDocumentReferences[i];

                try
                {
                    _logger.LogDebug(
                        "Processing DocumentReference {CurrentDoc}/{TotalDocs}: {DocumentReferenceId} [CorrelationId: {CorrelationId}]",
                        i + 1, allDocumentReferences.Count, documentReference.Id, correlationId);

                    // Directly process the DocumentReference we already have (no need to fetch it again)
                    DocumentDownloadResults documentResult
                        = await ProcessDocumentReferenceDirectAsync(
                            documentReference,
                            patientId,
                            overridePracticeId,
                            null, // Practice name will be determined from DocumentReference or practice ID
                            s3KeyPrefix,
                            s3Bucket,
                            cancellationToken);

                    results.Add(documentResult);

                    // Update summary statistics
                    totalFilesProcessed += documentResult.TotalFiles;
                    totalFilesSuccessful += documentResult.SuccessfulFiles;
                    totalBytesDownloaded += documentResult.TotalBytes;

                    // Log progress for every 10 documents or at completion
                    if ((i + 1) % 10 == 0 || i + 1 == allDocumentReferences.Count)
                    {
                        double currentProgress
                            = (double)(i + 1) / allDocumentReferences.Count * 100;
                        _logger.LogInformation(
                            "Download Progress: {Progress:F1}% ({Current}/{Total}) DocumentReferences processed, {FilesDownloaded} files downloaded ({BytesDownloaded:N0} bytes) [CorrelationId: {CorrelationId}]",
                            currentProgress, i + 1, allDocumentReferences.Count,
                            totalFilesSuccessful, totalBytesDownloaded, correlationId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to download DocumentReference {DocumentReferenceId} for Patient {PatientId} [CorrelationId: {CorrelationId}]",
                        documentReference.Id, patientId, correlationId);

                    // Create a failed result
                    var failedResult = new DocumentDownloadResults
                    {
                        DocumentReferenceId = documentReference.Id!,
                        StartTime = DateTime.UtcNow,
                        EndTime = DateTime.UtcNow,
                        IsSuccess = false,
                        ErrorMessage = ex.Message
                    };
                    results.Add(failedResult);
                }
            }

            // Log comprehensive summary
            TimeSpan downloadDuration = DateTime.UtcNow - downloadStartTime;
            int successfulDocuments = results.Count(r => r.Success);
            int failedDocuments = results.Count(r => !r.Success);

            _logger.LogInformation("Download Summary for Patient {PatientId}: " +
                                   "DocumentReferences processed: {TotalDocuments} (Success: {SuccessfulDocuments}, Failed: {FailedDocuments}), " +
                                   "Files processed: {TotalFiles} (Success: {SuccessfulFiles}, Failed: {FailedFiles}), " +
                                   "Total bytes downloaded: {TotalBytes:N0}, Duration: {Duration:hh\\:mm\\:ss} [CorrelationId: {CorrelationId}]",
                patientId, results.Count, successfulDocuments, failedDocuments,
                totalFilesProcessed, totalFilesSuccessful,
                totalFilesProcessed - totalFilesSuccessful,
                totalBytesDownloaded, downloadDuration, correlationId);

            // Report detailed errors for failed DocumentReferences
            LogDetailedErrorReport(results, patientId);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to get DocumentReferences for Patient {PatientId} [CorrelationId: {CorrelationId}]",
                patientId, correlationId);
            throw new DocumentDownloadException(
                $"Failed to get DocumentReferences for Patient {patientId}: {ex.Message}", ex,
                true);
        }
    }

    /// <summary>
    ///     Processes a DocumentReference directly (without fetching it again) and downloads its content to S3
    /// </summary>
    private async Task<DocumentDownloadResults> ProcessDocumentReferenceDirectAsync(
        DocumentReferenceData documentReference,
        string patientId,
        string? overridePracticeId,
        string? overridePracticeName,
        string? s3KeyPrefix,
        string? s3Bucket,
        CancellationToken cancellationToken)
    {
        string correlationId = _correlationIdProvider.GetCorrelationId();
        string documentReferenceId = documentReference.Id!;

        _logger.LogInformation(
            "Processing DocumentReference directly: {DocumentReferenceId} [CorrelationId: {CorrelationId}]",
            documentReferenceId, correlationId);

        var results = new DocumentDownloadResults
        {
            DocumentReferenceId = documentReferenceId,
            StartTime = DateTime.UtcNow
        };

        try
        {
            // Validate DocumentReference status
            if (documentReference.Status?.Equals("entered-in-error",
                    StringComparison.OrdinalIgnoreCase) == true)
            {
                _logger.LogWarning(
                    "Skipping DocumentReference {DocumentReferenceId} with status 'entered-in-error' [CorrelationId: {CorrelationId}]",
                    documentReferenceId, correlationId);

                results.EndTime = DateTime.UtcNow;
                results.IsSuccess = true; // This is considered a successful skip, not an error
                results.SkippedCount = 1;
                return results;
            }

            // Check if DocumentReference has any content
            if (documentReference.Content == null || documentReference.Content.Count == 0)
            {
                _logger.LogWarning(
                    "DocumentReference {DocumentReferenceId} has no content attachments to process [CorrelationId: {CorrelationId}]",
                    documentReferenceId, correlationId);

                return new DocumentDownloadResults
                {
                    DocumentReferenceId = documentReferenceId,
                    TotalFiles = 0,
                    SuccessfulFiles = 0,
                    FailedFiles = 0,
                    FileResults = new List<FileDownloadResult>(),
                    IsSuccess = true,
                    StartTime = DateTime.UtcNow,
                    EndTime = DateTime.UtcNow
                };
            }

            // Extract metadata and determine practice info
            string? practiceId = overridePracticeId ?? documentReference.GetPracticeInfo()?.Id;
            string? practiceName
                = overridePracticeName ?? documentReference.GetPracticeInfo()?.Name;

            // Process each content attachment
            var fileResults = new List<FileDownloadResult>();

            for (var attachmentIndex = 0;
                 attachmentIndex < documentReference.Content.Count;
                 attachmentIndex++)
            {
                DocumentContent content = documentReference.Content[attachmentIndex];
                Attachment? attachment = content.Attachment;

                if (attachment == null)
                {
                    _logger.LogWarning(
                        "Skipping content[{Index}] with null attachment in DocumentReference {DocumentReferenceId} [CorrelationId: {CorrelationId}]",
                        attachmentIndex, documentReferenceId, correlationId);
                    continue;
                }

                // Log that we're starting to process this specific file
                _logger.LogInformation(
                    "Processing file {FileNumber}/{TotalFiles} from DocumentReference {DocumentReferenceId}: " +
                    "ContentType={ContentType}, Title={Title}, IsEmbedded={IsEmbedded} [CorrelationId: {CorrelationId}]",
                    attachmentIndex + 1, documentReference.Content.Count, documentReferenceId,
                    attachment.ContentType ?? "unknown", attachment.Title ?? "untitled",
                    attachment.IsEmbeddedContent, correlationId);

                try
                {
                    FileDownloadResult fileResult = await ProcessAttachmentAsync(
                        documentReference,
                        attachment,
                        attachmentIndex,
                        patientId,
                        practiceId,
                        practiceName,
                        s3KeyPrefix,
                        s3Bucket,
                        cancellationToken);

                    fileResults.Add(fileResult);

                    // Log successful file processing
                    if (fileResult.Success)
                        _logger.LogInformation(
                            "Successfully processed file {FileNumber}/{TotalFiles}: " +
                            "S3Key={S3Key}, Size={SizeBytes} bytes, ContentType={ContentType} [CorrelationId: {CorrelationId}]",
                            attachmentIndex + 1, documentReference.Content.Count,
                            fileResult.S3Key, fileResult.SizeBytes, fileResult.ContentType,
                            correlationId);
                    else
                        _logger.LogWarning("Failed to process file {FileNumber}/{TotalFiles}: " +
                                           "Error={ErrorMessage} [CorrelationId: {CorrelationId}]",
                            attachmentIndex + 1, documentReference.Content.Count,
                            fileResult.ErrorMessage, correlationId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to process attachment[{Index}] for DocumentReference {DocumentReferenceId} [CorrelationId: {CorrelationId}]",
                        attachmentIndex, documentReferenceId, correlationId);

                    fileResults.Add(new FileDownloadResult
                    {
                        AttachmentIndex = attachmentIndex,
                        Success = false,
                        ErrorMessage = ex.Message,
                        ProcessingDuration = TimeSpan.Zero
                    });
                }
            }

            results.FileResults = fileResults;
            results.IsSuccess = fileResults.Any(f => f.Success);
            results.TotalFiles = fileResults.Count;
            results.SuccessfulFiles = fileResults.Count(f => f.Success);
            results.FailedFiles = fileResults.Count(f => !f.Success);
            results.TotalBytes = fileResults.Where(f => f.Success).Sum(f => f.SizeBytes);

            _logger.LogInformation(
                "DocumentReference processing completed: {DocumentReferenceId}, Success={Success}, Files={SuccessfulFiles}/{TotalFiles} [CorrelationId: {CorrelationId}]",
                documentReferenceId, results.Success, results.SuccessfulFiles, results.TotalFiles,
                correlationId);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "DocumentReference processing failed: {DocumentReferenceId} [CorrelationId: {CorrelationId}]",
                documentReferenceId, correlationId);

            results.IsSuccess = false;
            results.ErrorMessage = ex.Message;
            return results;
        }
        finally
        {
            results.EndTime = DateTime.UtcNow;
        }
    }

    /// <summary>
    ///     Downloads a specific DocumentReference and its content to S3
    /// </summary>
    /// <param name="documentReferenceId">The DocumentReference ID to download</param>
    /// <param name="overridePatientId">Optional patient ID override</param>
    /// <param name="overridePracticeId">Optional practice ID override</param>
    /// <param name="s3KeyPrefix">Optional S3 key prefix override</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Download results</returns>
    public async Task<DocumentDownloadResults> DownloadDocumentReferenceAsync(
        string documentReferenceId,
        string? overridePatientId = null,
        string? overridePracticeId = null,
        string? s3KeyPrefix = null,
        string? s3Bucket = null,
        CancellationToken cancellationToken = default)
    {
        string correlationId = _correlationIdProvider.GetCorrelationId();

        _logger.LogInformation(
            "Starting DocumentReference download: {DocumentReferenceId} [CorrelationId: {CorrelationId}]",
            documentReferenceId, correlationId);

        var results = new DocumentDownloadResults
        {
            DocumentReferenceId = documentReferenceId,
            StartTime = DateTime.UtcNow
        };

        try
        {
            // Step 1: Get DocumentReference using Patient/$everything if no patient ID provided
            DocumentReferenceData documentReference;
            if (!string.IsNullOrEmpty(overridePatientId))
            {
                // Use Patient/$everything to get the DocumentReference (with pagination)
                _logger.LogDebug(
                    "Retrieving DocumentReference {DocumentReferenceId} via Patient/$everything for Patient {PatientId} [CorrelationId: {CorrelationId}]",
                    documentReferenceId, overridePatientId, correlationId);

                IReadOnlyList<DocumentReferenceData> documentReferences
                    = await _patientEverythingService.GetPatientDocumentReferencesAsync(
                        overridePatientId, cancellationToken);

                documentReference
                    = documentReferences.FirstOrDefault(dr => dr.Id == documentReferenceId);
                if (documentReference == null)
                    throw new DocumentDownloadException(
                        $"DocumentReference {documentReferenceId} not found in Patient {overridePatientId} $everything response",
                        false, ErrorCategory.BusinessLogic);
            }
            else
            {
                // Direct DocumentReference retrieval
                _logger.LogDebug(
                    "Retrieving DocumentReference {DocumentReferenceId} directly from HealthLake [CorrelationId: {CorrelationId}]",
                    documentReferenceId, correlationId);

                try
                {
                    // Build the FHIR API URL for the DocumentReference
                    var requestUrl
                        = $"https://healthlake.{_healthLakeConfig.Region}.amazonaws.com/datastore/{_healthLakeConfig.DatastoreId}/r4/DocumentReference/{documentReferenceId}";

                    using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                    request.Headers.Add("Accept", "application/fhir+json");

                    using HttpResponseMessage response
                        = await _healthLakeHttpService.SendSignedRequestAsync(request,
                            cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        if (response.StatusCode == HttpStatusCode.NotFound)
                            throw new DocumentDownloadException(
                                $"DocumentReference {documentReferenceId} not found in HealthLake",
                                false, ErrorCategory.BusinessLogic);

                        string errorContent
                            = await response.Content.ReadAsStringAsync(cancellationToken);
                        throw new DocumentDownloadException(
                            $"HealthLake API returned {response.StatusCode}: {errorContent}",
                            true);
                    }

                    string documentReferenceJson
                        = await response.Content.ReadAsStringAsync(cancellationToken);

                    if (string.IsNullOrEmpty(documentReferenceJson))
                        throw new DocumentDownloadException(
                            $"Empty response from HealthLake for DocumentReference {documentReferenceId}");

                    // Parse the DocumentReference JSON into our data model
                    documentReference
                        = JsonSerializer.Deserialize<DocumentReferenceData>(documentReferenceJson);

                    if (documentReference == null)
                        throw new DocumentDownloadException(
                            $"Failed to parse DocumentReference JSON for {documentReferenceId}");

                    _logger.LogDebug(
                        "Successfully retrieved DocumentReference {DocumentReferenceId} directly [CorrelationId: {CorrelationId}]",
                        documentReferenceId, correlationId);
                }
                catch (Exception ex) when (!(ex is DocumentDownloadException))
                {
                    throw new DocumentDownloadException(
                        $"Failed to retrieve DocumentReference {documentReferenceId} directly from HealthLake: {ex.Message}",
                        ex, true);
                }
            }

            // Step 2: Validate DocumentReference status
            if (documentReference.Status?.Equals("entered-in-error",
                    StringComparison.OrdinalIgnoreCase) == true)
            {
                _logger.LogWarning(
                    "Skipping DocumentReference {DocumentReferenceId} with status 'entered-in-error' [CorrelationId: {CorrelationId}]",
                    documentReferenceId, correlationId);

                results.EndTime = DateTime.UtcNow;
                results.IsSuccess = true; // This is considered a successful skip, not an error
                results.SkippedCount = 1;
                return results;
            }

            // Step 3: Extract metadata and determine patient/practice info
            string? patientId = overridePatientId ?? documentReference.GetPatientId();
            PracticeInfo? practiceInfo = documentReference.GetPracticeInfo();
            string? practiceId = overridePracticeId ?? practiceInfo?.Id;

            if (string.IsNullOrEmpty(patientId))
                throw new DocumentDownloadException(
                    $"Could not determine patient ID for DocumentReference {documentReferenceId}",
                    false, ErrorCategory.BusinessLogic);

            // Step 3: Resolve practice name if needed
            string? practiceName = null;
            if (!string.IsNullOrEmpty(practiceId))
                practiceName
                    = await _bundleParserService.ResolvePracticeNameAsync(practiceId,
                        cancellationToken);

            // Step 4: Process each content attachment
            var fileResults = new List<FileDownloadResult>();

            for (var attachmentIndex = 0;
                 attachmentIndex < documentReference.Content.Count;
                 attachmentIndex++)
            {
                DocumentContent content = documentReference.Content[attachmentIndex];
                Attachment? attachment = content.Attachment;

                if (attachment == null)
                {
                    _logger.LogWarning(
                        "Skipping content[{Index}] with null attachment in DocumentReference {DocumentReferenceId} [CorrelationId: {CorrelationId}]",
                        attachmentIndex, documentReferenceId, correlationId);
                    continue;
                }

                // Log that we're starting to process this specific file
                _logger.LogInformation(
                    "Processing file {FileNumber}/{TotalFiles} from DocumentReference {DocumentReferenceId}: " +
                    "ContentType={ContentType}, Title={Title}, IsEmbedded={IsEmbedded} [CorrelationId: {CorrelationId}]",
                    attachmentIndex + 1, documentReference.Content.Count, documentReferenceId,
                    attachment.ContentType ?? "unknown", attachment.Title ?? "untitled",
                    attachment.IsEmbeddedContent, correlationId);

                try
                {
                    FileDownloadResult fileResult = await ProcessAttachmentAsync(
                        documentReference,
                        attachment,
                        attachmentIndex,
                        patientId,
                        practiceId,
                        practiceName,
                        s3KeyPrefix,
                        s3Bucket,
                        cancellationToken);

                    fileResults.Add(fileResult);

                    // Log successful file processing
                    if (fileResult.Success)
                        _logger.LogInformation(
                            "Successfully processed file {FileNumber}/{TotalFiles}: " +
                            "S3Key={S3Key}, Size={SizeBytes} bytes, ContentType={ContentType} [CorrelationId: {CorrelationId}]",
                            attachmentIndex + 1, documentReference.Content.Count,
                            fileResult.S3Key, fileResult.SizeBytes, fileResult.ContentType,
                            correlationId);
                    else
                        _logger.LogWarning("Failed to process file {FileNumber}/{TotalFiles}: " +
                                           "Error={ErrorMessage} [CorrelationId: {CorrelationId}]",
                            attachmentIndex + 1, documentReference.Content.Count,
                            fileResult.ErrorMessage, correlationId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to process attachment[{Index}] for DocumentReference {DocumentReferenceId} [CorrelationId: {CorrelationId}]",
                        attachmentIndex, documentReferenceId, correlationId);

                    fileResults.Add(new FileDownloadResult
                    {
                        AttachmentIndex = attachmentIndex,
                        Success = false,
                        ErrorMessage = ex.Message,
                        ProcessingDuration = TimeSpan.Zero
                    });
                }
            }

            results.FileResults = fileResults;
            results.IsSuccess = fileResults.Any(f => f.Success);
            results.TotalFiles = fileResults.Count;
            results.SuccessfulFiles = fileResults.Count(f => f.Success);
            results.FailedFiles = fileResults.Count(f => !f.Success);
            results.TotalBytes = fileResults.Where(f => f.Success).Sum(f => f.SizeBytes);

            _logger.LogInformation(
                "DocumentReference download completed: {DocumentReferenceId}, Success={Success}, Files={SuccessfulFiles}/{TotalFiles} [CorrelationId: {CorrelationId}]",
                documentReferenceId, results.Success, results.SuccessfulFiles, results.TotalFiles,
                correlationId);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "DocumentReference download failed: {DocumentReferenceId} [CorrelationId: {CorrelationId}]",
                documentReferenceId, correlationId);

            results.IsSuccess = false;
            results.ErrorMessage = ex.Message;
            return results;
        }
        finally
        {
            results.EndTime = DateTime.UtcNow;
        }
    }

    private async Task<FileDownloadResult> ProcessAttachmentAsync(
        DocumentReferenceData documentReference,
        Attachment attachment,
        int attachmentIndex,
        string patientId,
        string? practiceId,
        string? practiceName,
        string? s3KeyPrefix,
        string? s3Bucket,
        CancellationToken cancellationToken)
    {
        DateTime startTime = DateTime.UtcNow;
        string correlationId = _correlationIdProvider.GetCorrelationId();

        try
        {
            // Validate that attachment has either data or URL
            if (!attachment.IsEmbeddedContent && string.IsNullOrWhiteSpace(attachment.Url))
            {
                _logger.LogWarning(
                    "Attachment[{Index}] in DocumentReference {DocumentReferenceId} has neither data nor URL - skipping [CorrelationId: {CorrelationId}]",
                    attachmentIndex, documentReference.Id, correlationId);

                return new FileDownloadResult
                {
                    S3Key = null,
                    SizeBytes = 0,
                    ContentType = attachment.ContentType,
                    FileName = attachment.Title,
                    Success = false,
                    ErrorMessage = "Attachment has neither embedded data nor URL reference"
                };
            }

            // Extract metadata for S3 tags with resolved practice name and current attachment
            S3TagSet s3TagSet
                = _metadataExtractorService.ExtractMetadataToS3Tags(documentReference,
                    practiceName, attachment);

            if (attachment.IsEmbeddedContent)
                return await ProcessEmbeddedAttachmentAsync(
                    documentReference.Id,
                    attachment,
                    attachmentIndex,
                    patientId,
                    practiceId,
                    practiceName,
                    s3KeyPrefix,
                    s3Bucket,
                    s3TagSet,
                    startTime,
                    cancellationToken);

            return await ProcessBinaryAttachmentAsync(
                documentReference.Id,
                attachment,
                attachmentIndex,
                patientId,
                practiceId,
                practiceName,
                s3KeyPrefix,
                s3Bucket,
                s3TagSet,
                startTime,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing attachment[{Index}] for DocumentReference {DocumentReferenceId} [CorrelationId: {CorrelationId}]",
                attachmentIndex, documentReference.Id, correlationId);

            return new FileDownloadResult
            {
                AttachmentIndex = attachmentIndex,
                Success = false,
                ErrorMessage = ex.Message,
                ProcessingDuration = DateTime.UtcNow - startTime
            };
        }
    }

    private async Task<FileDownloadResult> ProcessEmbeddedAttachmentAsync(
        string documentReferenceId,
        Attachment attachment,
        int attachmentIndex,
        string patientId,
        string? practiceId,
        string? practiceName,
        string? s3KeyPrefix,
        string? s3Bucket,
        S3TagSet s3TagSet,
        DateTime startTime,
        CancellationToken cancellationToken)
    {
        string correlationId = _correlationIdProvider.GetCorrelationId();

        try
        {
            // Extract and decode base64 content
            DecodedContent decodedContent
                = await _base64ContentExtractor.ExtractAndDecodeAsync(attachment,
                    cancellationToken);

            // Generate S3 key for embedded content pat/docref....`
            string fileName = _fileOrganizationService.GenerateEmbeddedFileName(
                patientId,
                documentReferenceId,
                attachmentIndex,
                decodedContent.ContentType ?? "application/octet-stream");

            var practiceInfo = new PracticeInfo
            {
                Id = practiceId,
                Name = practiceName
            };

            string s3Key
                = _fileOrganizationService.GenerateS3Key(practiceInfo, patientId, fileName,
                    s3KeyPrefix);

            // Use the actual S3 bucket provided or fall back to default
            string actualS3Bucket = s3Bucket ?? _s3BucketName;

            // Upload the decoded binary data to S3
            using var memoryStream = new MemoryStream(decodedContent.Data);

            var putRequest = new PutObjectRequest
            {
                BucketName = actualS3Bucket,
                Key = s3Key,
                InputStream = memoryStream,
                ContentType = decodedContent.ContentType ??
                              attachment.ContentType ?? "application/octet-stream",
                StorageClass = S3StorageClass.StandardInfrequentAccess // TODO: Make configurable
            };

            // Add tags if provided
            if (s3TagSet?.Tags.Any() == true)
                putRequest.TagSet = s3TagSet.Tags
                    .Select(tag => new Tag { Key = tag.Key, Value = tag.Value }).ToList();

            // Set server-side encryption - TODO: Make configurable
            putRequest.ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256;

            PutObjectResponse? uploadResponse
                = await _s3Client.PutObjectAsync(putRequest, cancellationToken);

            // Log successful S3 upload with file details
            _logger.LogInformation("Successfully uploaded embedded file to S3: " +
                                   "FileName={FileName}, ContentType={ContentType}, Size={SizeBytes} bytes, " +
                                   "S3Key={S3Key}, S3Bucket={S3Bucket}, DocumentReferenceId={DocumentReferenceId}, AttachmentIndex={AttachmentIndex}, Duration={Duration}ms [CorrelationId: {CorrelationId}]",
                fileName, decodedContent.ContentType, decodedContent.SizeBytes,
                s3Key, actualS3Bucket, documentReferenceId, attachmentIndex,
                (DateTime.UtcNow - startTime).TotalMilliseconds, correlationId);

            return new FileDownloadResult
            {
                AttachmentIndex = attachmentIndex,
                S3Key = s3Key,
                SizeBytes = decodedContent.SizeBytes,
                ContentType = decodedContent.ContentType,
                FileName = decodedContent.Title,
                Success = true,
                IsEmbeddedContent = true,
                ProcessingDuration = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process embedded attachment[{Index}] for DocumentReference {DocumentReferenceId} [CorrelationId: {CorrelationId}]",
                attachmentIndex, documentReferenceId, correlationId);

            return new FileDownloadResult
            {
                AttachmentIndex = attachmentIndex,
                Success = false,
                ErrorMessage = $"Failed to process embedded attachment: {ex.Message}",
                IsEmbeddedContent = true,
                ProcessingDuration = DateTime.UtcNow - startTime
            };
        }
    }

    private async Task<FileDownloadResult> ProcessBinaryAttachmentAsync(
        string documentReferenceId,
        Attachment attachment,
        int attachmentIndex,
        string patientId,
        string? practiceId,
        string? practiceName,
        string? s3KeyPrefix,
        string? s3Bucket,
        S3TagSet s3TagSet,
        DateTime startTime,
        CancellationToken cancellationToken)
    {
        string? binaryId = attachment.GetBinaryId();
        if (string.IsNullOrEmpty(binaryId))
            throw new DocumentDownloadException(
                $"Could not extract Binary ID from attachment URL: {attachment.Url}",
                false, ErrorCategory.BusinessLogic);

        // Generate S3 key for binary content
        string fileName = _fileOrganizationService.GenerateBinaryFileName(
            patientId,
            documentReferenceId,
            attachmentIndex,
            attachment.Title,
            attachment.ContentType ?? "application/octet-stream");

        var practiceInfo = new PracticeInfo
        {
            Id = practiceId,
            Name = practiceName
        };

        string s3Key
            = _fileOrganizationService.GenerateS3Key(practiceInfo, patientId, fileName,
                s3KeyPrefix);

        // Download binary directly to S3
        BinaryDownloadResult downloadResult = await _binaryDownloadService.DownloadBinaryToS3Async(
            binaryId,
            s3Bucket ?? throw new ArgumentNullException(nameof(s3Bucket),
                "S3 bucket name is required for binary downloads"),
            s3Key,
            s3TagSet,
            patientId,
            documentReferenceId,
            cancellationToken);

        return new FileDownloadResult
        {
            AttachmentIndex = attachmentIndex,
            S3Key = downloadResult.S3Key,
            SizeBytes = downloadResult.SizeBytes,
            ContentType = downloadResult.ContentType,
            FileName = downloadResult.FileName,
            Success = true,
            IsEmbeddedContent = false,
            BinaryId = binaryId,
            S3ETag = downloadResult.S3ETag,
            UsedMultipartUpload = downloadResult.UsedMultipartUpload,
            ProcessingDuration = DateTime.UtcNow - startTime
        };
    }

    private void LogDetailedErrorReport(List<DocumentDownloadResults> results, string patientId)
    {
        List<DocumentDownloadResults> failedResults = results.Where(r => !r.Success).ToList();

        if (!failedResults.Any())
        {
            _logger.LogInformation(
                "All DocumentReferences processed successfully for patient {PatientId}", patientId);
            return;
        }

        _logger.LogError("=== DETAILED ERROR REPORT FOR PATIENT {PatientId} ===", patientId);
        _logger.LogError("Failed to process {FailedCount} out of {TotalCount} DocumentReferences",
            failedResults.Count, results.Count);

        foreach (DocumentDownloadResults result in failedResults)
        {
            _logger.LogError("--- DocumentReference: {DocumentReferenceId} ---",
                result.DocumentReferenceId);

            if (!string.IsNullOrEmpty(result.ErrorMessage))
                _logger.LogError("Error: {ErrorMessage}", result.ErrorMessage);

            // Log file-level failures if available
            if (result.FileResults?.Any() == true)
            {
                List<FileDownloadResult> failedFiles
                    = result.FileResults.Where(f => !f.Success).ToList();
                if (failedFiles.Any())
                {
                    _logger.LogError("Failed files ({FailedFileCount}/{TotalFileCount}):",
                        failedFiles.Count, result.FileResults.Count);

                    foreach (FileDownloadResult fileResult in failedFiles)
                    {
                        _logger.LogError(
                            "  - File: {FileName}, ContentType: {ContentType}, Error: {ErrorMessage}",
                            fileResult.FileName ?? "Unknown",
                            fileResult.ContentType ?? "Unknown",
                            fileResult.ErrorMessage ?? "Unknown error");

                        if (!string.IsNullOrEmpty(fileResult.BinaryId))
                            _logger.LogError("    Binary ID: {BinaryId}", fileResult.BinaryId);
                    }
                }
            }
            else
            {
                // Check common failure reasons
                if (result.ErrorMessage?.Contains("no content attachments") == true)
                    _logger.LogError("Issue: DocumentReference has no content attachments");
                else if (result.ErrorMessage?.Contains("no attachments with data or URL") == true)
                    _logger.LogError(
                        "Issue: DocumentReference has content attachments but none have data or URL");
                else if (result.ErrorMessage?.Contains("not found") == true)
                    _logger.LogError(
                        "Issue: DocumentReference or referenced Binary resource not found");
                else if (result.ErrorMessage?.Contains("access denied") == true ||
                         result.ErrorMessage?.Contains("unauthorized") == true)
                    _logger.LogError(
                        "Issue: Access denied when trying to retrieve DocumentReference or Binary");
                else if (result.ErrorMessage?.Contains("timeout") == true)
                    _logger.LogError("Issue: Request timeout when downloading");
                else
                    _logger.LogError("Issue: Unknown error occurred during processing");
            }

            _logger.LogError(""); // Empty line for readability
        }

        _logger.LogError("=== END ERROR REPORT FOR PATIENT {PatientId} ===", patientId);
    }
}

/// <summary>
///     Results of downloading a DocumentReference
/// </summary>
public class DocumentDownloadResults
{
    public required string DocumentReferenceId { get; set; }
    public bool IsSuccess { get; set; }
    public bool Success => IsSuccess; // Backwards compatibility
    public string? ErrorMessage { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration => EndTime - StartTime;
    public int TotalFiles { get; set; }
    public int SuccessfulFiles { get; set; }
    public int FailedFiles { get; set; }
    public int SkippedCount { get; set; }
    public long TotalBytes { get; set; }
    public List<FileDownloadResult> FileResults { get; set; } = new();
}

/// <summary>
///     Results of downloading a single file attachment
/// </summary>
public class FileDownloadResult
{
    public int AttachmentIndex { get; set; }
    public string? S3Key { get; set; }
    public long SizeBytes { get; set; }
    public string? ContentType { get; set; }
    public string? FileName { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsEmbeddedContent { get; set; }
    public string? BinaryId { get; set; }
    public string? S3ETag { get; set; }
    public bool UsedMultipartUpload { get; set; }
    public TimeSpan ProcessingDuration { get; set; }
}