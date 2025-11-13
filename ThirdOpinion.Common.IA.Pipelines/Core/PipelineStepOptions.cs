using System.Threading.Tasks.Dataflow;

namespace ThirdOpinion.Common.IA.Pipelines.Core;

/// <summary>
/// Options for configuring a pipeline step
/// </summary>
public class PipelineStepOptions
{
    /// <summary>
    /// Maximum degree of parallelism for the step (-1 = TPL unbounded default)
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; } = DataflowBlockOptions.Unbounded;

    /// <summary>
    /// Bounded capacity for the step's buffer
    /// </summary>
    public int BoundedCapacity { get; set; } = DataflowBlockOptions.Unbounded;

    /// <summary>
    /// Whether to enable progress tracking for this step
    /// </summary>
    public bool EnableProgressTracking { get; set; } = true;

    /// <summary>
    /// Create a copy of this options instance
    /// </summary>
    public PipelineStepOptions Clone()
    {
        return new PipelineStepOptions
        {
            MaxDegreeOfParallelism = MaxDegreeOfParallelism,
            BoundedCapacity = BoundedCapacity,
            EnableProgressTracking = EnableProgressTracking
        };
    }
}

