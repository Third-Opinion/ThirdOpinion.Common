using System.ComponentModel.DataAnnotations;

namespace ThirdOpinion.Common.Logging.Configuration;

/// <summary>
///     Configuration settings for transaction logging
/// </summary>
public class TransactionLogConfig
{
    /// <summary>
    ///     Directory where transaction log files are stored
    /// </summary>
    [Required]
    public string LogDirectory { get; set; } = "./logs/transactions/";

    /// <summary>
    ///     Maximum file size in megabytes before rotation
    /// </summary>
    [Range(1, 1000)]
    public int MaxFileSizeMB { get; set; } = 100;

    /// <summary>
    ///     Number of days to retain log files
    /// </summary>
    [Range(1, 365)]
    public int RetentionDays { get; set; } = 30;

    /// <summary>
    ///     Whether transaction logging is enabled
    /// </summary>
    public bool EnableLogging { get; set; } = true;

    /// <summary>
    ///     Log file rotation mode
    /// </summary>
    public LogRotationMode RotationMode { get; set; } = LogRotationMode.Daily;

    /// <summary>
    ///     Whether to compress rotated log files
    /// </summary>
    public bool CompressRotatedFiles { get; set; } = true;

    /// <summary>
    ///     Buffer size for batching log writes (number of entries)
    /// </summary>
    [Range(1, 1000)]
    public int BufferSize { get; set; } = 50;

    /// <summary>
    ///     Flush interval in seconds for buffered writes
    /// </summary>
    [Range(1, 300)]
    public int FlushIntervalSeconds { get; set; } = 30;

    /// <summary>
    ///     Date format pattern for log file names
    /// </summary>
    [Required]
    public string DateFormat { get; set; } = "yyyy-MM-dd";

    /// <summary>
    ///     File name prefix for transaction log files
    /// </summary>
    [Required]
    public string FileNamePrefix { get; set; } = "transactions";

    /// <summary>
    ///     Whether to include metadata in log entries
    /// </summary>
    public bool IncludeMetadata { get; set; } = true;

    /// <summary>
    ///     Maximum metadata size in characters
    /// </summary>
    [Range(100, 10000)]
    public int MaxMetadataSize { get; set; } = 2000;
}

/// <summary>
///     Log file rotation modes
/// </summary>
public enum LogRotationMode
{
    /// <summary>
    ///     Create new log file each day
    /// </summary>
    Daily,

    /// <summary>
    ///     Create new log file each week
    /// </summary>
    Weekly,

    /// <summary>
    ///     Create new log file each month
    /// </summary>
    Monthly,

    /// <summary>
    ///     Rotate based on file size only
    /// </summary>
    SizeBased
}