namespace ThirdOpinion.Common.DataFlow.Core;

/// <summary>
/// Factory for creating pipeline context builders with dependency injection
/// </summary>
public interface IPipelineContextFactory
{
    /// <summary>
    /// Create a new pipeline context builder for the specified resource type
    /// </summary>
    /// <typeparam name="TResource">Type of resource being processed</typeparam>
    /// <returns>A fluent builder for configuring the pipeline context</returns>
    PipelineContextBuilder CreateBuilder<TResource>();

    /// <summary>
    /// Create a new pipeline context builder for the specified resource type
    /// </summary>
    /// <param name="resourceType">Type of resource being processed</param>
    /// <returns>A fluent builder for configuring the pipeline context</returns>
    PipelineContextBuilder CreateBuilder(Type resourceType);
}

