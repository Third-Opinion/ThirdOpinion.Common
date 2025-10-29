using Microsoft.Extensions.Logging;

namespace ThirdOpinion.Common.Logging;

public static partial class LoggerMessageDefinitions
{
    // Application lifecycle
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "FhirTools application started with correlation ID {CorrelationId}")]
    public static partial void ApplicationStarted(this ILogger logger, string correlationId);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Information,
        Message = "FhirTools application completed successfully in {ElapsedTime}ms")]
    public static partial void ApplicationCompleted(this ILogger logger, long elapsedTime);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Error,
        Message = "FhirTools application failed with error: {ErrorMessage}")]
    public static partial void ApplicationFailed(this ILogger logger, string errorMessage, Exception exception);

    // Configuration
    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "Configuration loaded successfully - Athena URL: {AthenaUrl}, HealthLake Region: {HealthLakeRegion}")]
    public static partial void ConfigurationLoaded(this ILogger logger, string athenaUrl, string healthLakeRegion);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Error,
        Message = "Configuration validation failed: {ValidationErrors}")]
    public static partial void ConfigurationValidationFailed(this ILogger logger, string validationErrors);

    // OAuth and Authentication
    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Debug,
        Message = "OAuth token request initiated for client {ClientId}")]
    public static partial void OAuthTokenRequestStarted(this ILogger logger, string clientId);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Information,
        Message = "OAuth token acquired successfully, expires in {ExpiresInSeconds} seconds")]
    public static partial void OAuthTokenAcquired(this ILogger logger, int expiresInSeconds);

    [LoggerMessage(
        EventId = 3003,
        Level = LogLevel.Warning,
        Message = "OAuth token refresh triggered {SecondsBeforeExpiry} seconds before expiry")]
    public static partial void OAuthTokenRefreshTriggered(this ILogger logger, int secondsBeforeExpiry);

    [LoggerMessage(
        EventId = 3004,
        Level = LogLevel.Error,
        Message = "OAuth authentication failed with status {StatusCode}: {ErrorDescription}")]
    public static partial void OAuthAuthenticationFailed(this ILogger logger, int statusCode, string errorDescription);

    // CSV Processing
    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Information,
        Message = "Started processing CSV file: {FilePath}, starting from row {StartRow}")]
    public static partial void CsvProcessingStarted(this ILogger logger, string filePath, int startRow);

    [LoggerMessage(
        EventId = 4002,
        Level = LogLevel.Information,
        Message = "Processing batch {BatchNumber}: rows {StartRow}-{EndRow} ({BatchSize} resources)")]
    public static partial void BatchProcessingStarted(this ILogger logger, int batchNumber, int startRow, int endRow, int batchSize);

    [LoggerMessage(
        EventId = 4003,
        Level = LogLevel.Information,
        Message = "Batch {BatchNumber} completed successfully in {ElapsedTime}ms")]
    public static partial void BatchProcessingCompleted(this ILogger logger, int batchNumber, long elapsedTime);

    [LoggerMessage(
        EventId = 4004,
        Level = LogLevel.Warning,
        Message = "Invalid resource ID format at row {RowNumber}: {ResourceId}")]
    public static partial void InvalidResourceIdFormat(this ILogger logger, int rowNumber, string resourceId);

    // FHIR Resource Operations
    [LoggerMessage(
        EventId = 5001,
        Level = LogLevel.Debug,
        Message = "Fetching FHIR resource: {ResourceType}/{ResourceId} from Athena")]
    public static partial void FhirResourceFetchStarted(this ILogger logger, string resourceType, string resourceId);

    [LoggerMessage(
        EventId = 5002,
        Level = LogLevel.Debug,
        Message = "FHIR resource fetched successfully: {ResourceType}/{ResourceId} ({SizeBytes} bytes)")]
    public static partial void FhirResourceFetched(this ILogger logger, string resourceType, string resourceId, int sizeBytes);

    [LoggerMessage(
        EventId = 5003,
        Level = LogLevel.Debug,
        Message = "Writing FHIR resource to HealthLake: {ResourceType}/{ResourceId}")]
    public static partial void FhirResourceWriteStarted(this ILogger logger, string resourceType, string resourceId);

    [LoggerMessage(
        EventId = 5004,
        Level = LogLevel.Debug,
        Message = "FHIR resource written successfully to HealthLake: {ResourceType}/{ResourceId}")]
    public static partial void FhirResourceWritten(this ILogger logger, string resourceType, string resourceId);

    // Rate Limiting and Retry
    [LoggerMessage(
        EventId = 6001,
        Level = LogLevel.Debug,
        Message = "Rate limit applied: waiting {DelayMs}ms before next request")]
    public static partial void RateLimitApplied(this ILogger logger, int delayMs);

    [LoggerMessage(
        EventId = 6002,
        Level = LogLevel.Warning,
        Message = "Retry attempt {AttemptNumber}/{MaxAttempts} for operation {OperationType} after {DelayMs}ms delay")]
    public static partial void RetryAttempt(this ILogger logger, int attemptNumber, int maxAttempts, string operationType, int delayMs);

    [LoggerMessage(
        EventId = 6003,
        Level = LogLevel.Error,
        Message = "Circuit breaker opened for {ServiceName} after {FailureCount} consecutive failures")]
    public static partial void CircuitBreakerOpened(this ILogger logger, string serviceName, int failureCount);
    
    [LoggerMessage(
        EventId = 6004,
        Level = LogLevel.Information,
        Message = "Rate limiter initialized for {ServiceName}: {CallsPerSecond:F2} calls/sec, burst size {BurstSize}")]
    public static partial void RateLimiterInitialized(this ILogger logger, string serviceName, double callsPerSecond, int burstSize);
    
    [LoggerMessage(
        EventId = 6005,
        Level = LogLevel.Warning,
        Message = "Rate limiter for {ServiceName} is throttling: {WaitingRequests} requests waiting, {AvailableTokens} tokens available")]
    public static partial void RateLimiterThrottling(this ILogger logger, string serviceName, int waitingRequests, int availableTokens);
    
    [LoggerMessage(
        EventId = 6006,
        Level = LogLevel.Debug,
        Message = "Rate limiter for {ServiceName} acquired token: {AvailableTokens} tokens remaining")]
    public static partial void RateLimiterTokenAcquired(this ILogger logger, string serviceName, int availableTokens);
    
    [LoggerMessage(
        EventId = 6007,
        Level = LogLevel.Information,
        Message = "Rate limit updated for {ServiceName}: {OldRate:F2} -> {NewRate:F2} calls/sec")]
    public static partial void RateLimitUpdated(this ILogger logger, string serviceName, double oldRate, double newRate);

    // Progress and Checkpoints
    [LoggerMessage(
        EventId = 7001,
        Level = LogLevel.Information,
        Message = "Progress update: {ProcessedCount}/{TotalCount} resources processed ({PercentComplete:F1}%)")]
    public static partial void ProgressUpdate(this ILogger logger, int processedCount, int totalCount, double percentComplete);

    [LoggerMessage(
        EventId = 7002,
        Level = LogLevel.Information,
        Message = "Checkpoint saved: row {RowNumber}, processed {ProcessedCount} resources")]
    public static partial void CheckpointSaved(this ILogger logger, int rowNumber, int processedCount);

    [LoggerMessage(
        EventId = 7003,
        Level = LogLevel.Information,
        Message = "Resuming from checkpoint: row {RowNumber}, previously processed {ProcessedCount} resources")]
    public static partial void ResumedFromCheckpoint(this ILogger logger, int rowNumber, int processedCount);
}