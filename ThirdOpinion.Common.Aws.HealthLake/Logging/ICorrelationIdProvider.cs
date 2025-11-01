namespace ThirdOpinion.Common.Aws.HealthLake.Logging;

/// <summary>
///     Provides correlation IDs for request tracking
/// </summary>
public interface ICorrelationIdProvider
{
    /// <summary>
    ///     Gets the current correlation ID
    /// </summary>
    string GetCorrelationId();
}