namespace ThirdOpinion.Common.DataFlow.Results;

/// <summary>
/// Result wrapper for explicit error propagation through pipeline without exceptions
/// </summary>
/// <typeparam name="T">Type of the wrapped value</typeparam>
public class PipelineResult<T>
{
    /// <summary>
    /// Whether the operation succeeded
    /// </summary>
    public bool IsSuccess { get; private set; }

    /// <summary>
    /// The wrapped value (only valid if IsSuccess is true)
    /// </summary>
    public T? Value { get; private set; }

    /// <summary>
    /// Error message if operation failed
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Name of the step where the error occurred
    /// </summary>
    public string? ErrorStep { get; private set; }

    /// <summary>
    /// Duration of the operation in milliseconds
    /// </summary>
    public int? DurationMs { get; private set; }

    /// <summary>
    /// Resource ID associated with this result
    /// </summary>
    public string ResourceId { get; private set; } = string.Empty;

    /// <summary>
    /// Create a successful result
    /// </summary>
    public static PipelineResult<T> Success(T value, string resourceId, int? durationMs = null)
    {
        return new PipelineResult<T>
        {
            IsSuccess = true,
            Value = value,
            ResourceId = resourceId,
            DurationMs = durationMs
        };
    }

    /// <summary>
    /// Create a failed result
    /// </summary>
    public static PipelineResult<T> Failure(
        string resourceId, 
        string errorMessage, 
        string? errorStep = null, 
        T? partialValue = default)
    {
        return new PipelineResult<T>
        {
            IsSuccess = false,
            Value = partialValue,
            ResourceId = resourceId,
            ErrorMessage = errorMessage,
            ErrorStep = errorStep
        };
    }

    /// <summary>
    /// Map the value to a new type if successful, otherwise propagate the error
    /// </summary>
    public PipelineResult<TNew> Map<TNew>(Func<T, TNew> mapper)
    {
        if (!IsSuccess)
        {
            return PipelineResult<TNew>.Failure(
                ResourceId, 
                ErrorMessage ?? "Unknown error", 
                ErrorStep);
        }

        try
        {
            var newValue = mapper(Value!);
            return PipelineResult<TNew>.Success(newValue, ResourceId, DurationMs);
        }
        catch (Exception ex)
        {
            return PipelineResult<TNew>.Failure(ResourceId, ex.Message, "Map");
        }
    }

    /// <summary>
    /// Async map the value to a new type if successful, otherwise propagate the error
    /// </summary>
    public async Task<PipelineResult<TNew>> MapAsync<TNew>(Func<T, Task<TNew>> mapper)
    {
        if (!IsSuccess)
        {
            return PipelineResult<TNew>.Failure(
                ResourceId, 
                ErrorMessage ?? "Unknown error", 
                ErrorStep);
        }

        try
        {
            var newValue = await mapper(Value!);
            return PipelineResult<TNew>.Success(newValue, ResourceId, DurationMs);
        }
        catch (Exception ex)
        {
            return PipelineResult<TNew>.Failure(ResourceId, ex.Message, "MapAsync");
        }
    }
}

