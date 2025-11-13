using Microsoft.Extensions.Logging.Abstractions;
using ThirdOpinion.Common.IA.Pipelines.Core;

namespace ThirdOpinion.Common.IA.Pipelines.Examples;

/// <summary>
/// Example of a simple transformation pipeline without progress tracking or artifacts
/// </summary>
public class SimpleTransformPipeline
{
    public record InputData(string Id, string Name, int Value);
    public record ProcessedData(string Id, string Name, int DoubledValue, DateTime ProcessedAt);
    public record EnrichedData(string Id, string Name, int DoubledValue, string Category, DateTime ProcessedAt);

    public static async Task RunExample()
    {
        // Sample data
        var inputData = new List<InputData>
        {
            new("1", "Item A", 10),
            new("2", "Item B", 20),
            new("3", "Item C", 30),
            new("4", "Item D", 40),
            new("5", "Item E", 50)
        };

        // Create minimal context (no progress tracking or artifacts)
        var context = new PipelineContext(
            runId: Guid.NewGuid(),
            resourceType: typeof(InputData),
            cancellationToken: CancellationToken.None,
            logger: NullLogger.Instance);

        // Build and execute pipeline
        var results = new List<EnrichedData>();

        await DataFlowPipeline<InputData>
            .Create(context, data => data.Id)
            .FromEnumerable(inputData)
            .Transform(ProcessData, "Process")
            .Transform(EnrichData, "Enrich")
            .Action(data =>
            {
                results.Add(data);
                return Task.CompletedTask;
            }, "Collect")
            .Complete();

        // Display results
        Console.WriteLine("Pipeline completed successfully!");
        Console.WriteLine($"Processed {results.Count} items:");
        foreach (var result in results)
        {
            Console.WriteLine($"  {result.Id}: {result.Name} - {result.Category} (Value: {result.DoubledValue})");
        }
    }

    private static async Task<ProcessedData> ProcessData(InputData input)
    {
        // Simulate async processing
        await Task.Delay(10);
        
        return new ProcessedData(
            input.Id,
            input.Name,
            input.Value * 2,
            DateTime.UtcNow);
    }

    private static async Task<EnrichedData> EnrichData(ProcessedData processed)
    {
        // Simulate async enrichment
        await Task.Delay(10);
        
        var category = processed.DoubledValue switch
        {
            < 50 => "Low",
            < 100 => "Medium",
            _ => "High"
        };

        return new EnrichedData(
            processed.Id,
            processed.Name,
            processed.DoubledValue,
            category,
            processed.ProcessedAt);
    }
}

