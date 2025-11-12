namespace ThirdOpinion.Common.DataFlow.EntityFramework;

/// <summary>
/// Options that control the behaviour of the pipeline context pool when leasing EF Core DbContext instances.
/// </summary>
public class PipelineContextPoolOptions
{
    private int _maxConcurrentContexts = 16;

    /// <summary>
    /// Maximum number of concurrent DbContext instances that can be leased at once.
    /// </summary>
    public int MaxConcurrentContexts
    {
        get => _maxConcurrentContexts;
        set => _maxConcurrentContexts = value <= 0 ? throw new ArgumentOutOfRangeException(nameof(value), "Value must be greater than zero.") : value;
    }
}


