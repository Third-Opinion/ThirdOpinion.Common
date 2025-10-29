namespace ThirdOpinion.Common.Fhir.Documents;

/// <summary>
/// Service for tracking Binary resources that return 404 NotFound errors
/// </summary>
public interface INotFoundBinaryTracker
{
    /// <summary>
    /// Records a Binary resource that was not found during download
    /// </summary>
    /// <param name="binaryId">The Binary resource ID</param>
    /// <param name="fullUrl">The complete URL that was requested</param>
    /// <param name="patientId">Optional patient ID associated with the binary</param>
    /// <param name="documentReferenceId">Optional DocumentReference ID that referenced this binary</param>
    /// <param name="correlationId">Correlation ID for tracing</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task TrackNotFoundBinaryAsync(
        string binaryId,
        string fullUrl,
        string? patientId = null,
        string? documentReferenceId = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the path to the NotFound binaries log file
    /// </summary>
    string GetNotFoundLogFilePath();
}