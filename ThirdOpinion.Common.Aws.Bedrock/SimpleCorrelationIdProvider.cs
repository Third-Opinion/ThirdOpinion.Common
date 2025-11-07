using ThirdOpinion.Common.Logging;

namespace ThirdOpinion.Common.Aws.Bedrock;

/// <summary>
///     Simple implementation of ICorrelationIdProvider for Bedrock service
/// </summary>
internal class SimpleCorrelationIdProvider : ICorrelationIdProvider
{
    private readonly AsyncLocal<string> _correlationId = new();

    public string GetCorrelationId()
    {
        return _correlationId.Value ?? Guid.NewGuid().ToString();
    }

    public IDisposable BeginScope(string? correlationId)
    {
        string? previousValue = _correlationId.Value;
        _correlationId.Value = correlationId ?? GetCorrelationId();
        return new CorrelationScope(() => _correlationId.Value = previousValue!);
    }

    public void SetCorrelationId(string correlationId)
    {
        _correlationId.Value = correlationId;
    }

    private class CorrelationScope : IDisposable
    {
        private readonly Action _onDispose;

        public CorrelationScope(Action onDispose)
        {
            _onDispose = onDispose;
        }

        public void Dispose()
        {
            _onDispose();
        }
    }
}