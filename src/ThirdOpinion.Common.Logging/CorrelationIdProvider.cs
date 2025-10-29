using Microsoft.Extensions.Logging;

namespace ThirdOpinion.Common.Logging;

public interface ICorrelationIdProvider
{
    string GetCorrelationId();
    IDisposable BeginScope(string? correlationId = null);
    void SetCorrelationId(string correlationId);
}

public class CorrelationIdProvider : ICorrelationIdProvider
{
    private static readonly AsyncLocal<string> _correlationId = new();
    private readonly ILogger<CorrelationIdProvider> _logger;

    public CorrelationIdProvider(ILogger<CorrelationIdProvider> logger)
    {
        _logger = logger;
    }

    public string GetCorrelationId()
    {
        return _correlationId.Value ?? GenerateCorrelationId();
    }

    public void SetCorrelationId(string correlationId)
    {
        _correlationId.Value = correlationId;
    }

    public IDisposable BeginScope(string? correlationId = null)
    {
        var id = correlationId ?? GenerateCorrelationId();
        _correlationId.Value = id;
        
        return _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = id
        }) ?? new NullDisposable();
    }

    private class NullDisposable : IDisposable
    {
        public void Dispose() { }
    }

    private static string GenerateCorrelationId()
    {
        return Guid.NewGuid().ToString("N")[..8];
    }
}