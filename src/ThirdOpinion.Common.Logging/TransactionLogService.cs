using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using CsvHelper;
using ThirdOpinion.Common.Logging.Configuration;
using ThirdOpinion.Common.Logging.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ThirdOpinion.Common.Logging;

/// <summary>
/// Service for logging and managing transaction entries with file-based storage and rotation
/// </summary>
public class TransactionLogService : ITransactionLogService, IDisposable
{
    private readonly ILogger<TransactionLogService> _logger;
    private readonly TransactionLogConfig _config;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly object _writeLock = new();
    private readonly ConcurrentQueue<TransactionLogEntry> _buffer = new();
    private readonly Timer _flushTimer;
    private bool _disposed;

    public TransactionLogService(
        ILogger<TransactionLogService> logger,
        IOptions<TransactionLogConfig> config)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        // Ensure log directory exists
        if (_config.EnableLogging)
        {
            Directory.CreateDirectory(_config.LogDirectory);
        }

        // Set up periodic flush timer
        _flushTimer = new Timer(
            async _ => await FlushBufferAsync(),
            null,
            TimeSpan.FromSeconds(_config.FlushIntervalSeconds),
            TimeSpan.FromSeconds(_config.FlushIntervalSeconds));
    }

    /// <inheritdoc />
    public async Task LogTransactionAsync(TransactionLogEntry entry, CancellationToken cancellationToken = default)
    {
        if (!_config.EnableLogging)
        {
            return;
        }

        try
        {
            // Truncate metadata if it exceeds the configured size
            if (entry.Metadata != null && _config.IncludeMetadata)
            {
                var metadataJson = JsonSerializer.Serialize(entry.Metadata, _jsonOptions);
                if (metadataJson.Length > _config.MaxMetadataSize)
                {
                    entry.Metadata = new Dictionary<string, object>
                    {
                        { "truncated", true },
                        { "originalSize", metadataJson.Length },
                        { "message", "Metadata truncated due to size limit" }
                    };
                }
            }
            else if (!_config.IncludeMetadata)
            {
                entry.Metadata = null;
            }

            _buffer.Enqueue(entry);

            // Flush if buffer is full
            if (_buffer.Count >= _config.BufferSize)
            {
                await FlushBufferAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging transaction entry for DocumentId: {DocumentId}", entry.DocumentId);
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TransactionLogEntry>> GetTransactionsAsync(string runId, CancellationToken cancellationToken = default)
    {
        var transactions = new List<TransactionLogEntry>();

        try
        {
            var logFiles = Directory.GetFiles(_config.LogDirectory, $"{_config.FileNamePrefix}*.json");

            foreach (var logFile in logFiles)
            {
                var fileTransactions = await ReadTransactionsFromFileAsync(logFile, cancellationToken);
                transactions.AddRange(fileTransactions.Where(t => t.RunId == runId));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving transactions for RunId: {RunId}", runId);
        }

        return transactions.OrderBy(t => t.Timestamp);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TransactionLogEntry>> GetTransactionsByDateRangeAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        var transactions = new List<TransactionLogEntry>();

        try
        {
            var logFiles = Directory.GetFiles(_config.LogDirectory, $"{_config.FileNamePrefix}*.json");

            foreach (var logFile in logFiles)
            {
                var fileTransactions = await ReadTransactionsFromFileAsync(logFile, cancellationToken);
                transactions.AddRange(fileTransactions.Where(t => t.Timestamp >= fromDate && t.Timestamp <= toDate));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving transactions for date range: {FromDate} to {ToDate}", fromDate, toDate);
        }

        return transactions.OrderBy(t => t.Timestamp);
    }

    /// <inheritdoc />
    public async Task ExportToCsvAsync(string filePath, string? runId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var transactions = runId != null
                ? await GetTransactionsAsync(runId, cancellationToken)
                : await GetAllTransactionsAsync(cancellationToken);

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var writer = new StreamWriter(filePath);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

            await csv.WriteRecordsAsync(transactions, cancellationToken);

            _logger.LogInformation("Exported {Count} transactions to CSV: {FilePath}",
                transactions.Count(), filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting transactions to CSV: {FilePath}", filePath);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task ExportToJsonAsync(string filePath, string? runId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var transactions = runId != null
                ? await GetTransactionsAsync(runId, cancellationToken)
                : await GetAllTransactionsAsync(cancellationToken);

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(transactions, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json, cancellationToken);

            _logger.LogInformation("Exported {Count} transactions to JSON: {FilePath}",
                transactions.Count(), filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting transactions to JSON: {FilePath}", filePath);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<TransactionSummary> GetTransactionSummaryAsync(string? runId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var transactions = runId != null
                ? await GetTransactionsAsync(runId, cancellationToken)
                : await GetAllTransactionsAsync(cancellationToken);

            var transactionsList = transactions.ToList();
            var completedTransactions = transactionsList
                .Where(t => t.Duration.HasValue)
                .ToList();

            return new TransactionSummary
            {
                TotalTransactions = transactionsList.Count,
                SuccessfulTransactions = transactionsList.Count(t => t.Status == TransactionStatus.Success),
                FailedTransactions = transactionsList.Count(t => t.Status == TransactionStatus.Failed),
                PendingTransactions = transactionsList.Count(t => t.Status == TransactionStatus.Pending),
                ProcessingTransactions = transactionsList.Count(t => t.Status == TransactionStatus.Processing),
                SkippedTransactions = transactionsList.Count(t => t.Status == TransactionStatus.Skipped),
                AverageProcessingDuration = completedTransactions.Any()
                    ? TimeSpan.FromTicks((long)completedTransactions.Average(t => t.Duration!.Value.Ticks))
                    : null,
                RunId = runId,
                FromDate = transactionsList.Any() ? transactionsList.Min(t => t.Timestamp) : null,
                ToDate = transactionsList.Any() ? transactionsList.Max(t => t.Timestamp) : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating transaction summary for RunId: {RunId}", runId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> CleanupOldLogsAsync(CancellationToken cancellationToken = default)
    {
        var cleanupCount = 0;

        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-_config.RetentionDays);
            var logFiles = Directory.GetFiles(_config.LogDirectory, $"{_config.FileNamePrefix}*.json");

            foreach (var logFile in logFiles)
            {
                var fileInfo = new FileInfo(logFile);
                if (fileInfo.CreationTimeUtc < cutoffDate)
                {
                    // Compress before deletion if configured
                    if (_config.CompressRotatedFiles)
                    {
                        await CompressLogFileAsync(logFile, cancellationToken);
                    }

                    File.Delete(logFile);
                    cleanupCount++;
                    _logger.LogInformation("Cleaned up old log file: {LogFile}", logFile);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during log cleanup");
        }

        return cleanupCount;
    }

    private async Task FlushBufferAsync(CancellationToken cancellationToken = default)
    {
        if (_buffer.IsEmpty)
        {
            return;
        }

        var entriesToFlush = new List<TransactionLogEntry>();

        // Dequeue all current entries
        while (_buffer.TryDequeue(out var entry))
        {
            entriesToFlush.Add(entry);
        }

        if (entriesToFlush.Count == 0)
        {
            return;
        }

        lock (_writeLock)
        {
            try
            {
                var logFileName = GetLogFileName();
                var logFilePath = Path.Combine(_config.LogDirectory, logFileName);

                // Check if rotation is needed
                if (ShouldRotateLog(logFilePath))
                {
                    RotateLogFile(logFilePath);
                }

                // Append entries to log file
                AppendEntriesToFile(logFilePath, entriesToFlush);

                _logger.LogDebug("Flushed {Count} transaction entries to {LogFile}",
                    entriesToFlush.Count, logFileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error flushing transaction log buffer");

                // Re-queue entries to avoid data loss
                foreach (var entry in entriesToFlush)
                {
                    _buffer.Enqueue(entry);
                }
            }
        }
    }

    private async Task<List<TransactionLogEntry>> ReadTransactionsFromFileAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var transactions = JsonSerializer.Deserialize<List<TransactionLogEntry>>(json, _jsonOptions);
            return transactions ?? new List<TransactionLogEntry>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error reading transactions from file: {FilePath}", filePath);
            return new List<TransactionLogEntry>();
        }
    }

    private async Task<IEnumerable<TransactionLogEntry>> GetAllTransactionsAsync(CancellationToken cancellationToken)
    {
        var transactions = new List<TransactionLogEntry>();

        try
        {
            var logFiles = Directory.GetFiles(_config.LogDirectory, $"{_config.FileNamePrefix}*.json");

            foreach (var logFile in logFiles)
            {
                var fileTransactions = await ReadTransactionsFromFileAsync(logFile, cancellationToken);
                transactions.AddRange(fileTransactions);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all transactions");
        }

        return transactions.OrderBy(t => t.Timestamp);
    }

    private string GetLogFileName()
    {
        var dateFormat = _config.RotationMode switch
        {
            LogRotationMode.Daily => "yyyy-MM-dd",
            LogRotationMode.Weekly => "yyyy-'W'ww",
            LogRotationMode.Monthly => "yyyy-MM",
            LogRotationMode.SizeBased => _config.DateFormat,
            _ => _config.DateFormat
        };

        var datePart = DateTime.UtcNow.ToString(dateFormat);
        return $"{_config.FileNamePrefix}-{datePart}.json";
    }

    private bool ShouldRotateLog(string logFilePath)
    {
        if (!File.Exists(logFilePath))
        {
            return false;
        }

        if (_config.RotationMode == LogRotationMode.SizeBased)
        {
            var fileInfo = new FileInfo(logFilePath);
            var maxSizeBytes = _config.MaxFileSizeMB * 1024 * 1024;
            return fileInfo.Length >= maxSizeBytes;
        }

        return false; // Time-based rotation is handled by filename generation
    }

    private void RotateLogFile(string logFilePath)
    {
        try
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var rotatedPath = logFilePath.Replace(".json", $"-{timestamp}.json");

            File.Move(logFilePath, rotatedPath);

            if (_config.CompressRotatedFiles)
            {
                _ = Task.Run(async () => await CompressLogFileAsync(rotatedPath));
            }

            _logger.LogInformation("Rotated log file: {OriginalPath} -> {RotatedPath}", logFilePath, rotatedPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rotating log file: {LogFilePath}", logFilePath);
        }
    }

    private void AppendEntriesToFile(string logFilePath, List<TransactionLogEntry> entries)
    {
        List<TransactionLogEntry> existingEntries = new();

        // Read existing entries if file exists
        if (File.Exists(logFilePath))
        {
            try
            {
                var existingJson = File.ReadAllText(logFilePath);
                existingEntries = JsonSerializer.Deserialize<List<TransactionLogEntry>>(existingJson, _jsonOptions) ?? new();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error reading existing log file, starting fresh: {LogFilePath}", logFilePath);
            }
        }

        // Combine existing and new entries
        existingEntries.AddRange(entries);

        // Write back to file
        var json = JsonSerializer.Serialize(existingEntries, _jsonOptions);
        File.WriteAllText(logFilePath, json);
    }

    private async Task CompressLogFileAsync(string logFilePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var compressedPath = logFilePath + ".gz";

            using var originalFileStream = new FileStream(logFilePath, FileMode.Open, FileAccess.Read);
            using var compressedFileStream = new FileStream(compressedPath, FileMode.Create, FileAccess.Write);
            using var compressionStream = new GZipStream(compressedFileStream, CompressionLevel.Optimal);

            await originalFileStream.CopyToAsync(compressionStream, cancellationToken);

            File.Delete(logFilePath);
            _logger.LogInformation("Compressed log file: {LogFilePath} -> {CompressedPath}", logFilePath, compressedPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error compressing log file: {LogFilePath}", logFilePath);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            _flushTimer?.Dispose();

            // Final flush
            FlushBufferAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during TransactionLogService disposal");
        }

        _disposed = true;
    }
}