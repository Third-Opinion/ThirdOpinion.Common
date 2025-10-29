using System.Text.Json.Serialization;

namespace ThirdOpinion.Common.Logging.Models;

/// <summary>
/// Transaction log entry for tracking document processing operations
/// </summary>
public class TransactionLogEntry
{
    /// <summary>
    /// Timestamp when the transaction occurred
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Unique identifier for the document being processed
    /// </summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>
    /// Name of the prompt used for processing
    /// </summary>
    public string PromptName { get; set; } = string.Empty;

    /// <summary>
    /// Unique identifier for the processing run/session
    /// </summary>
    public string RunId { get; set; } = string.Empty;

    /// <summary>
    /// Current status of the transaction
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;

    /// <summary>
    /// Error message if the transaction failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Name of the S3 bucket where files are stored
    /// </summary>
    public string? S3BucketName { get; set; }

    /// <summary>
    /// Name of the input file being processed
    /// </summary>
    public string? InputFileName { get; set; }

    /// <summary>
    /// Name of the output file generated
    /// </summary>
    public string? OutputFileName { get; set; }

    /// <summary>
    /// Duration of the processing operation
    /// </summary>
    public TimeSpan? Duration { get; set; }

    /// <summary>
    /// Name/identifier of the AI model used for processing
    /// </summary>
    public string? ModelUsed { get; set; }

    /// <summary>
    /// Unique request identifier for tracking API calls
    /// </summary>
    public string? RequestId { get; set; }

    /// <summary>
    /// HTTP response status code from the processing operation
    /// </summary>
    public int? ResponseStatusCode { get; set; }

    /// <summary>
    /// Additional metadata for the transaction
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Status of a transaction log entry
/// </summary>
public enum TransactionStatus
{
    /// <summary>
    /// Transaction is queued but not yet started
    /// </summary>
    Pending,

    /// <summary>
    /// Transaction is currently being processed
    /// </summary>
    Processing,

    /// <summary>
    /// Transaction completed successfully
    /// </summary>
    Success,

    /// <summary>
    /// Transaction failed with an error
    /// </summary>
    Failed,

    /// <summary>
    /// Transaction was skipped (e.g., already processed)
    /// </summary>
    Skipped
}