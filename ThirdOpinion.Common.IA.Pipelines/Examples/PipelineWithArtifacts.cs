using Microsoft.Extensions.Logging.Abstractions;
using ThirdOpinion.Common.IA.Pipelines.Core;
using ThirdOpinion.Common.IA.Pipelines.Models;
using ThirdOpinion.Common.IA.Pipelines.Services.InMemory;

namespace ThirdOpinion.Common.IA.Pipelines.Examples;

/// <summary>
/// Example of a pipeline with artifact capture
/// </summary>
public class PipelineWithArtifacts
{
    public record Document(string Id, string Content);
    public record ExtractedFacts(string DocumentId, List<string> Facts);
    public record AnalysisResult(string DocumentId, List<string> Facts, Dictionary<string, int> WordCounts);

    public static async Task RunExample()
    {
        var documents = new List<Document>
        {
            new("doc1", "The quick brown fox jumps over the lazy dog"),
            new("doc2", "Machine learning is transforming healthcare"),
            new("doc3", "Climate change affects global temperatures")
        };

        // Create context with artifact storage using factory
        var context = InMemoryServiceFactory.CreateContextWithArtifacts<Document>(
            category: "Examples",
            name: "PipelineWithArtifacts",
            cancellationToken: CancellationToken.None);
        
        var artifactStorage = new InMemoryArtifactStorageService();

        var results = new List<AnalysisResult>();

        await DataFlowPipeline<Document>
            .Create(context, doc => doc.Id)
            .FromEnumerable(documents)
            .Transform(ExtractFacts, "ExtractFacts")
                .WithArtifact(
                    artifactNameFactory: f => $"facts_{f.DocumentId}.json",
                    storageType: ArtifactStorageType.Memory)
            .Transform(AnalyzeWords, "AnalyzeWords")
                .WithArtifact(
                    artifactNameFactory: a => $"analysis_{a.DocumentId}.json",
                    storageType: ArtifactStorageType.Memory)
            .Action(result =>
            {
                results.Add(result);
                return Task.CompletedTask;
            }, "Collect")
            .Complete();

        // Display results
        Console.WriteLine("Pipeline completed with artifacts!");
        Console.WriteLine($"\nProcessed {results.Count} documents:");
        foreach (var result in results)
        {
            Console.WriteLine($"  {result.DocumentId}:");
            Console.WriteLine($"    Facts: {string.Join(", ", result.Facts)}");
            Console.WriteLine($"    Top words: {string.Join(", ", result.WordCounts.Take(3).Select(kvp => $"{kvp.Key}({kvp.Value})"))}");
        }

        Console.WriteLine($"\nArtifacts saved: {artifactStorage.GetArtifactCount()}");
    }

    private static async Task<ExtractedFacts> ExtractFacts(Document doc)
    {
        await Task.Delay(10);
        
        // Simple fact extraction (split by words)
        var words = doc.Content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var facts = words.Where(w => w.Length > 5).Take(3).ToList();

        return new ExtractedFacts(doc.Id, facts);
    }

    private static async Task<AnalysisResult> AnalyzeWords(ExtractedFacts facts)
    {
        await Task.Delay(10);
        
        // Simple word frequency analysis
        var wordCounts = facts.Facts
            .GroupBy(f => f.ToLower())
            .ToDictionary(g => g.Key, g => g.Count())
            .OrderByDescending(kvp => kvp.Value)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        return new AnalysisResult(facts.DocumentId, facts.Facts, wordCounts);
    }
}

