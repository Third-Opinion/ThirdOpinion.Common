using ThirdOpinion.Common.Logging.Models;

namespace ThirdOpinion.Common.Logging;

/// <summary>
///     Service for logging and managing transaction entries for document processing operations
/// </summary>
public interface ITransactionLogService
{
    /// <summary>
    ///     Log a transaction entry asynchronously
    /// </summary>
    /// <param name="entry">Transaction log entry to record</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task LogTransactionAsync(TransactionLogEntry entry,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Retrieve transaction entries by run ID
    /// </summary>
    /// <param name="runId">Unique run identifier to filter by</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of transaction entries for the specified run</returns>
    Task<IEnumerable<TransactionLogEntry>> GetTransactionsAsync(string runId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Retrieve all transaction entries within a date range
    /// </summary>
    /// <param name="fromDate">Start date for filtering</param>
    /// <param name="toDate">End date for filtering</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of transaction entries within the date range</returns>
    Task<IEnumerable<TransactionLogEntry>> GetTransactionsByDateRangeAsync(DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Export transaction logs to CSV format
    /// </summary>
    /// <param name="filePath">Output file path for the CSV export</param>
    /// <param name="runId">Optional run ID to filter by specific processing run</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async export operation</returns>
    Task ExportToCsvAsync(string filePath,
        string? runId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Export transaction logs to JSON format
    /// </summary>
    /// <param name="filePath">Output file path for the JSON export</param>
    /// <param name="runId">Optional run ID to filter by specific processing run</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async export operation</returns>
    Task ExportToJsonAsync(string filePath,
        string? runId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get summary statistics for transactions
    /// </summary>
    /// <param name="runId">Optional run ID to filter by specific processing run</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Transaction statistics summary</returns>
    Task<TransactionSummary> GetTransactionSummaryAsync(string? runId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Clean up old transaction logs based on retention policy
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of log entries cleaned up</returns>
    Task<int> CleanupOldLogsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
///     Summary statistics for transaction logs
/// </summary>
public class TransactionSummary
{
    /// <summary>
    ///     Total number of transactions
    /// </summary>
    public int TotalTransactions { get; set; }

    /// <summary>
    ///     Number of successful transactions
    /// </summary>
    public int SuccessfulTransactions { get; set; }

    /// <summary>
    ///     Number of failed transactions
    /// </summary>
    public int FailedTransactions { get; set; }

    /// <summary>
    ///     Number of pending transactions
    /// </summary>
    public int PendingTransactions { get; set; }

    /// <summary>
    ///     Number of transactions currently being processed
    /// </summary>
    public int ProcessingTransactions { get; set; }

    /// <summary>
    ///     Number of skipped transactions
    /// </summary>
    public int SkippedTransactions { get; set; }

    /// <summary>
    ///     Average processing duration for completed transactions
    /// </summary>
    public TimeSpan? AverageProcessingDuration { get; set; }

    /// <summary>
    ///     Success rate as a percentage (0-100)
    /// </summary>
    public double SuccessRate => TotalTransactions > 0
        ? SuccessfulTransactions * 100.0 / TotalTransactions
        : 0.0;

    /// <summary>
    ///     Run ID this summary applies to (null for all runs)
    /// </summary>
    public string? RunId { get; set; }

    /// <summary>
    ///     Date range covered by this summary
    /// </summary>
    public DateTime? FromDate { get; set; }

    /// <summary>
    ///     Date range covered by this summary
    /// </summary>
    public DateTime? ToDate { get; set; }
}