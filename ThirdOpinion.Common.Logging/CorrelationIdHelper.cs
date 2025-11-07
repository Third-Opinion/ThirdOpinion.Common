namespace ThirdOpinion.Common.Logging;

/// <summary>
///     Static helper for correlation ID access throughout the application
/// </summary>
public static class CorrelationIdHelper
{
    private static readonly AsyncLocal<string> _correlationId = new();

    /// <summary>
    ///     Get the current correlation ID from AsyncLocal storage
    /// </summary>
    public static string GetCurrentCorrelationId()
    {
        return _correlationId.Value ?? "no-correlation-id";
    }

    /// <summary>
    ///     Set the correlation ID in AsyncLocal storage
    /// </summary>
    public static void SetCorrelationId(string correlationId)
    {
        _correlationId.Value = correlationId;
    }
}