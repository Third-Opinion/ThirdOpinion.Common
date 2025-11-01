using Microsoft.Extensions.Logging;
using ThirdOpinion.Common.Logging;

namespace ThirdOpinion.Common.Fhir.Documents;

/// <summary>
///     Service for tracking Binary resources that return 404 NotFound errors
/// </summary>
public class NotFoundBinaryTracker : INotFoundBinaryTracker
{
    private readonly ICorrelationIdProvider _correlationIdProvider;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly ILogger<NotFoundBinaryTracker> _logger;
    private readonly string _notFoundLogFilePath;

    public NotFoundBinaryTracker(
        ILogger<NotFoundBinaryTracker> logger,
        ICorrelationIdProvider correlationIdProvider)
    {
        _logger = logger;
        _correlationIdProvider = correlationIdProvider;

        // Create log file in current directory with timestamp
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        _notFoundLogFilePath = Path.Combine(Directory.GetCurrentDirectory(),
            $"not_found_binaries_{timestamp}.csv");

        // Ensure the CSV file has headers
        EnsureHeadersExist();
    }

    public async Task TrackNotFoundBinaryAsync(
        string binaryId,
        string fullUrl,
        string? patientId = null,
        string? documentReferenceId = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        correlationId ??= _correlationIdProvider.GetCorrelationId();

        _logger.LogWarning(
            "Tracking NotFound binary: {BinaryId} at URL: {FullUrl} [PatientId: {PatientId}] [DocRefId: {DocumentReferenceId}] [CorrelationId: {CorrelationId}]",
            binaryId, fullUrl, patientId ?? "N/A", documentReferenceId ?? "N/A", correlationId);

        var record = new NotFoundBinaryRecord
        {
            Timestamp = DateTime.UtcNow,
            BinaryId = binaryId,
            FullUrl = fullUrl,
            PatientId = patientId,
            DocumentReferenceId = documentReferenceId,
            CorrelationId = correlationId
        };

        await WriteToCsvFileAsync(record, cancellationToken);
    }

    public string GetNotFoundLogFilePath()
    {
        return _notFoundLogFilePath;
    }

    private void EnsureHeadersExist()
    {
        if (!File.Exists(_notFoundLogFilePath))
        {
            var headers = "Timestamp,BinaryId,FullUrl,PatientId,DocumentReferenceId,CorrelationId";
            File.WriteAllText(_notFoundLogFilePath, headers + Environment.NewLine);

            _logger.LogInformation("Created NotFound binaries log file: {LogFilePath}",
                _notFoundLogFilePath);
        }
    }

    private async Task WriteToCsvFileAsync(NotFoundBinaryRecord record,
        CancellationToken cancellationToken)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            string csvLine = $"{record.Timestamp:yyyy-MM-dd HH:mm:ss.fff}," +
                             $"\"{EscapeCsvValue(record.BinaryId)}\"," +
                             $"\"{EscapeCsvValue(record.FullUrl)}\"," +
                             $"\"{EscapeCsvValue(record.PatientId ?? "")}\"," +
                             $"\"{EscapeCsvValue(record.DocumentReferenceId ?? "")}\"," +
                             $"\"{EscapeCsvValue(record.CorrelationId ?? "")}\"";

            await File.AppendAllTextAsync(_notFoundLogFilePath, csvLine + Environment.NewLine,
                cancellationToken);

            _logger.LogDebug("Wrote NotFound binary record to CSV: {BinaryId}", record.BinaryId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to write NotFound binary record to CSV file: {LogFilePath}",
                _notFoundLogFilePath);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private static string EscapeCsvValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        // Escape quotes by doubling them
        return value.Replace("\"", "\"\"");
    }

    private class NotFoundBinaryRecord
    {
        public DateTime Timestamp { get; set; }
        public required string BinaryId { get; set; }
        public required string FullUrl { get; set; }
        public string? PatientId { get; set; }
        public string? DocumentReferenceId { get; set; }
        public string? CorrelationId { get; set; }
    }
}