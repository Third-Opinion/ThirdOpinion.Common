using ThirdOpinion.Common.DataFlow.Artifacts;
using ThirdOpinion.Common.DataFlow.Core;
using ThirdOpinion.Common.DataFlow.Models;
using ThirdOpinion.Common.DataFlow.Services.InMemory;
using ThirdOpinion.DataFlow.Artifacts.Models;
using static ThirdOpinion.Common.DataFlow.Tests.TestTimings;

namespace ThirdOpinion.Common.DataFlow.Tests;

/// <summary>
/// Integration tests for artifact capture functionality
/// </summary>
public class ArtifactCaptureTests
{
    private record TestData(string Id, string Content);
    private record ProcessedData(string Id, string ProcessedContent);
    private record EnrichedData(string Id, string ProcessedContent, int WordCount);

    [Fact]
    public async Task WithArtifact_CapturesArtifacts()
    {
        // Arrange
        var input = new[]
        {
            new TestData("doc-1", "Hello world"),
            new TestData("doc-2", "Testing artifacts")
        };

        // Use slow storage to test drain mechanism without slowing the suite down
        var innerStorage = new InMemoryArtifactStorageService();
        var slowStorage = new SlowArtifactStorageService(innerStorage, delayMs: SlowStorageDelayMs);
        var batcherFactory = new InMemoryArtifactBatcherFactory(slowStorage, flushIntervalMs: ArtifactFlushBufferMs);
        
        var builder = new PipelineContextBuilder(
            resourceType: typeof(TestData),
            progressTrackerFactory: null,
            artifactBatcherFactory: batcherFactory,
            resourceRunCache: new InMemoryResourceRunCache());
        
        var context = builder
            .WithCategory("Test")
            .WithName("ArtifactCapture")
            .WithCancellationToken(CancellationToken.None)
            .Build();

        // Act
        await DataFlowPipeline<TestData>
            .Create(context, d => d.Id)
            .FromEnumerable(input)
            .Transform(data => Task.FromResult(new ProcessedData(data.Id, data.Content.ToUpper())), "Process")
                .WithArtifact(artifactNameFactory: d => $"{d.Id}_processed.json")
            .Action(_ => Task.CompletedTask, "DoNothing")
            .Complete();

        // Assert - artifacts should be saved even with slow storage due to drain mechanism
        Assert.Equal(2, innerStorage.GetArtifactCount());
        var keys = innerStorage.GetAllKeys();
        Assert.Contains(keys, k => k.Contains("doc-1"));
        Assert.Contains(keys, k => k.Contains("doc-2"));
    }

    [Fact]
    public async Task MultipleArtifacts_CapturesAll()
    {
        // Arrange
        var input = new[] { new TestData("test-1", "hello world") };

        var storage = new InMemoryArtifactStorageService();
        var batcherFactory = new InMemoryArtifactBatcherFactory(storage, flushIntervalMs: ArtifactFlushBufferMs);
        
        var builder = new PipelineContextBuilder(
            resourceType: typeof(TestData),
            progressTrackerFactory: null,
            artifactBatcherFactory: batcherFactory,
            resourceRunCache: new InMemoryResourceRunCache());
        
        var context = builder
            .WithCategory("Test")
            .WithName("ArtifactCapture")
            .WithCancellationToken(CancellationToken.None)
            .Build();

        // Act
        await DataFlowPipeline<TestData>
            .Create(context, d => d.Id)
            .FromEnumerable(input)
            .Transform(data => Task.FromResult(new ProcessedData(data.Id, data.Content.ToUpper())), "Process")
                .WithArtifact(artifactNameFactory: d => $"{d.Id}_step1.json")
            .Transform(data => Task.FromResult(new EnrichedData(data.Id, data.ProcessedContent, data.ProcessedContent.Split(' ').Length)), "Enrich")
                .WithArtifact(artifactNameFactory: d => $"{d.Id}_step2.json")
            .Action(_ => Task.CompletedTask, "DoNothing")
            .Complete();

        // Assert
        Assert.Equal(2, storage.GetArtifactCount());
    }

    [Fact]
    public async Task WithArtifact_WithoutBatcher_DoesNotFail()
    {
        // Arrange
        var input = new[] { new TestData("test-1", "hello") };

        var context = new PipelineContext(
            Guid.NewGuid(),
            typeof(TestData),
            CancellationToken.None);

        var results = new System.Collections.Concurrent.ConcurrentBag<ProcessedData>();

        // Act & Assert - Should not throw
        await DataFlowPipeline<TestData>
            .Create(context, d => d.Id)
            .FromEnumerable(input)
            .Transform(data => Task.FromResult(new ProcessedData(data.Id, data.Content.ToUpper())), "Process")
                .WithArtifact(artifactName: "output.json") // Should be ignored gracefully
            .Action(result =>
            {
                results.Add(result);
                return Task.CompletedTask;
            }, "Collect")
            .Complete();

        Assert.Single(results);
    }

    [Fact]
    public async Task CustomArtifactData_CapturesCorrectly()
    {
        // Arrange
        var input = new[] { new TestData("test-1", "hello world") };

        var storage = new InMemoryArtifactStorageService();
        var batcherFactory = new InMemoryArtifactBatcherFactory(storage, flushIntervalMs: ArtifactFlushBufferMs);
        
        var builder = new PipelineContextBuilder(
            resourceType: typeof(TestData),
            progressTrackerFactory: null,
            artifactBatcherFactory: batcherFactory,
            resourceRunCache: new InMemoryResourceRunCache());
        
        var context = builder
            .WithCategory("Test")
            .WithName("ArtifactCapture")
            .WithCancellationToken(CancellationToken.None)
            .Build();

        // Act
        await DataFlowPipeline<TestData>
            .Create(context, d => d.Id)
            .FromEnumerable(input)
            .Transform(data => Task.FromResult(new EnrichedData(data.Id, data.Content, data.Content.Length)), "Process")
                .WithArtifact(
                    artifactNameFactory: d => $"{d.Id}_custom.json",
                    getArtifactData: d => new { d.Id, d.WordCount }) // Custom subset
            .Action(_ => Task.CompletedTask, "DoNothing")
            .Complete();

        // Assert
        Assert.Equal(1, storage.GetArtifactCount());
        var artifact = storage.GetArtifact(storage.GetAllKeys().First());
        Assert.NotNull(artifact);
        Assert.Contains("WordCount", artifact);
    }

    [Fact]
    public async Task WithArtifact_DifferentStorageTypes_Works()
    {
        // Arrange
        var input = new[] { new TestData("test-1", "hello") };

        var storage = new InMemoryArtifactStorageService();
        var batcherFactory = new InMemoryArtifactBatcherFactory(storage, flushIntervalMs: ArtifactFlushBufferMs);
        
        var builder = new PipelineContextBuilder(
            resourceType: typeof(TestData),
            progressTrackerFactory: null,
            artifactBatcherFactory: batcherFactory,
            resourceRunCache: new InMemoryResourceRunCache());
        
        var context = builder
            .WithCategory("Test")
            .WithName("ArtifactCapture")
            .WithCancellationToken(CancellationToken.None)
            .Build();

        // Act
        await DataFlowPipeline<TestData>
            .Create(context, d => d.Id)
            .FromEnumerable(input)
            .Transform(data => Task.FromResult(new ProcessedData(data.Id, data.Content)), "Process")
                .WithArtifact(
                    artifactName: "output_s3.json",
                    storageType: ArtifactStorageType.S3)
            .Transform(data => Task.FromResult(new EnrichedData(data.Id, data.ProcessedContent, 1)), "Enrich")
                .WithArtifact(
                    artifactName: "output_db.json",
                    storageType: ArtifactStorageType.Database)
            .Action(_ => Task.CompletedTask, "DoNothing")
            .Complete();

        // Assert
        Assert.Equal(2, storage.GetArtifactCount());
    }

    [Theory]
    [InlineData(50, 1)]    // 50 items, 1ms processing
    [InlineData(100, 5)]   // 100 items, 5ms processing
    [InlineData(200, 10)]  // 200 items, 10ms processing
    [InlineData(500, 2)]   // 500 items, 2ms processing
    public async Task ArtifactCapture_StressTest_VariousSizesAndDelays(int itemCount, int delayMs)
    {
        // Arrange
        var storage = new InMemoryArtifactStorageService();
        var batcherFactory = new InMemoryArtifactBatcherFactory(storage, flushIntervalMs: 100);
        
        var builder = new PipelineContextBuilder(
            resourceType: typeof(TestData),
            progressTrackerFactory: null,
            artifactBatcherFactory: batcherFactory,
            resourceRunCache: new InMemoryResourceRunCache());
        
        var context = builder
            .WithCategory("StressTest")
            .WithName($"Items{itemCount}_Delay{delayMs}")
            .WithCancellationToken(CancellationToken.None)
            .Build();

        var input = Enumerable.Range(1, itemCount)
            .Select(i => new TestData($"item-{i}", $"Content {i}"))
            .ToArray();

        // Act
        await DataFlowPipeline<TestData>
            .Create(context, d => d.Id)
            .FromEnumerable(input)
            .Transform(async data =>
            {
                if (delayMs > 0)
                    await Task.Delay(delayMs);
                return new ProcessedData(data.Id, data.Content.ToUpper());
            }, "Process")
                .WithArtifact(artifactNameFactory: d => $"{d.Id}_processed.json")
            .Action(_ => Task.CompletedTask, "Complete")
            .Complete();

        // Assert
        Assert.Equal(itemCount, storage.GetArtifactCount());
        
        var keys = storage.GetAllKeys();
        for (int i = 1; i <= itemCount; i++)
        {
            Assert.Contains(keys, k => k.Contains($"item-{i}"));
        }
    }

    [Fact]
    public async Task ArtifactCapture_WithSlowStorage_LargeDataset()
    {
        // Arrange - Large dataset with slow storage
        var innerStorage = new InMemoryArtifactStorageService();
        var slowStorage = new SlowArtifactStorageService(innerStorage, delayMs: 200);
        var batcherFactory = new InMemoryArtifactBatcherFactory(slowStorage, flushIntervalMs: 150);
        
        var builder = new PipelineContextBuilder(
            resourceType: typeof(TestData),
            progressTrackerFactory: null,
            artifactBatcherFactory: batcherFactory,
            resourceRunCache: new InMemoryResourceRunCache());
        
        var context = builder
            .WithCategory("StressTest")
            .WithName("SlowStorageLarge")
            .WithCancellationToken(CancellationToken.None)
            .Build();

        const int itemCount = 100;
        var input = Enumerable.Range(1, itemCount)
            .Select(i => new TestData($"large-{i}", $"Large content block {i}"))
            .ToArray();

        // Act
        await DataFlowPipeline<TestData>
            .Create(context, d => d.Id)
            .FromEnumerable(input)
            .Transform(async data =>
            {
                await Task.Delay(2); // Minimal processing delay
                return new ProcessedData(data.Id, data.Content.ToUpper());
            }, "Process")
                .WithArtifact(artifactNameFactory: d => $"{d.Id}_processed.json")
            .Action(_ => Task.CompletedTask, "Complete")
            .Complete();

        // Assert - All artifacts should be saved despite slow storage
        Assert.Equal(itemCount, innerStorage.GetArtifactCount());
        
        var keys = innerStorage.GetAllKeys();
        for (int i = 1; i <= itemCount; i++)
        {
            Assert.Contains(keys, k => k.Contains($"large-{i}"));
        }
    }

    [Fact]
    public async Task ArtifactCapture_MultipleSteps_LargeDataset()
    {
        // Arrange
        var storage = new InMemoryArtifactStorageService();
        var batcherFactory = new InMemoryArtifactBatcherFactory(storage, flushIntervalMs: 100);
        
        var builder = new PipelineContextBuilder(
            resourceType: typeof(TestData),
            progressTrackerFactory: null,
            artifactBatcherFactory: batcherFactory,
            resourceRunCache: new InMemoryResourceRunCache());
        
        var context = builder
            .WithCategory("StressTest")
            .WithName("MultiStepLarge")
            .WithCancellationToken(CancellationToken.None)
            .Build();

        const int itemCount = 150;
        var input = Enumerable.Range(1, itemCount)
            .Select(i => new TestData($"multi-{i}", $"Content {i}"))
            .ToArray();

        // Act - Multiple transform steps with artifacts
        await DataFlowPipeline<TestData>
            .Create(context, d => d.Id)
            .FromEnumerable(input)
            .Transform(async data =>
            {
                await Task.Delay(1);
                return new ProcessedData(data.Id, data.Content.ToUpper());
            }, "Step1")
                .WithArtifact(artifactNameFactory: d => $"{d.Id}_step1.json")
            .Transform(async data =>
            {
                await Task.Delay(1);
                return new EnrichedData(data.Id, data.ProcessedContent, 1);
            }, "Step2")
                .WithArtifact(artifactNameFactory: d => $"{d.Id}_step2.json")
            .Action(_ => Task.CompletedTask, "Complete")
            .Complete();

        // Assert - Should have artifacts from both steps
        Assert.Equal(itemCount * 2, storage.GetArtifactCount());
        
        var keys = storage.GetAllKeys();
        for (int i = 1; i <= itemCount; i++)
        {
            Assert.Contains(keys, k => k.Contains($"multi-{i}_step1"));
            Assert.Contains(keys, k => k.Contains($"multi-{i}_step2"));
        }
    }

    [Fact]
    public async Task ArtifactCapture_HighConcurrency_MixedProcessingTimes()
    {
        // Arrange
        var storage = new InMemoryArtifactStorageService();
        var batcherFactory = new InMemoryArtifactBatcherFactory(storage, flushIntervalMs: 50);
        
        var builder = new PipelineContextBuilder(
            resourceType: typeof(TestData),
            progressTrackerFactory: null,
            artifactBatcherFactory: batcherFactory,
            resourceRunCache: new InMemoryResourceRunCache());
        
        var context = builder
            .WithCategory("StressTest")
            .WithName("HighConcurrencyMixed")
            .WithCancellationToken(CancellationToken.None)
            .Build();

        const int itemCount = 200;
        var random = new Random(42); // Fixed seed for reproducibility
        var input = Enumerable.Range(1, itemCount)
            .Select(i => new TestData($"concurrent-{i}", $"Content {i}"))
            .ToArray();

        // Act - Simulate varying processing times
        await DataFlowPipeline<TestData>
            .Create(context, d => d.Id)
            .FromEnumerable(input)
            .Transform(async data =>
            {
                // Random delay between 0-20ms to simulate real-world variance
                var delay = random.Next(0, 21);
                if (delay > 0)
                    await Task.Delay(delay);
                return new ProcessedData(data.Id, data.Content.ToUpper());
            }, "VariableProcess")
                .WithArtifact(artifactNameFactory: d => $"{d.Id}_processed.json")
            .Action(_ => Task.CompletedTask, "Complete")
            .Complete();

        // Assert
        Assert.Equal(itemCount, storage.GetArtifactCount());
        
        var keys = storage.GetAllKeys();
        for (int i = 1; i <= itemCount; i++)
        {
            Assert.Contains(keys, k => k.Contains($"concurrent-{i}"));
        }
    }

    [Fact]
    public async Task ArtifactCapture_VeryLargeDataset_MinimalDelay()
    {
        // Arrange - Test with very large dataset
        var storage = new InMemoryArtifactStorageService();
        var batcherFactory = new InMemoryArtifactBatcherFactory(storage, flushIntervalMs: 100);
        
        var builder = new PipelineContextBuilder(
            resourceType: typeof(TestData),
            progressTrackerFactory: null,
            artifactBatcherFactory: batcherFactory,
            resourceRunCache: new InMemoryResourceRunCache());
        
        var context = builder
            .WithCategory("StressTest")
            .WithName("VeryLarge")
            .WithCancellationToken(CancellationToken.None)
            .Build();

        const int itemCount = 1000;
        var input = Enumerable.Range(1, itemCount)
            .Select(i => new TestData($"bulk-{i}", $"Content {i}"))
            .ToArray();

        // Act
        await DataFlowPipeline<TestData>
            .Create(context, d => d.Id)
            .FromEnumerable(input)
            .Transform(data => Task.FromResult(new ProcessedData(data.Id, data.Content.ToUpper())), "FastProcess")
                .WithArtifact(artifactNameFactory: d => $"{d.Id}_processed.json")
            .Action(_ => Task.CompletedTask, "Complete")
            .Complete();

        // Assert
        Assert.Equal(itemCount, storage.GetArtifactCount());
        
        // Verify a sample of items rather than all to keep test fast
        var keys = storage.GetAllKeys().ToList();
        Assert.Contains(keys, k => k.Contains("bulk-1"));
        Assert.Contains(keys, k => k.Contains("bulk-500"));
        Assert.Contains(keys, k => k.Contains("bulk-1000"));
    }

    [Fact]
    public async Task ArtifactCapture_RepeatTest_EnsuresConsistency()
    {
        // Run the original test scenario multiple times to catch intermittent issues
        for (int iteration = 1; iteration <= 10; iteration++)
        {
            var innerStorage = new InMemoryArtifactStorageService();
            var slowStorage = new SlowArtifactStorageService(innerStorage, delayMs: SlowStorageDelayMs);
            var batcherFactory = new InMemoryArtifactBatcherFactory(slowStorage, flushIntervalMs: ArtifactFlushBufferMs);
            
            var builder = new PipelineContextBuilder(
                resourceType: typeof(TestData),
                progressTrackerFactory: null,
                artifactBatcherFactory: batcherFactory,
                resourceRunCache: new InMemoryResourceRunCache());
            
            var context = builder
                .WithCategory("ConsistencyTest")
                .WithName($"Iteration{iteration}")
                .WithCancellationToken(CancellationToken.None)
                .Build();

            var input = new[]
            {
                new TestData("doc-1", "Hello world"),
                new TestData("doc-2", "Testing artifacts")
            };

            await DataFlowPipeline<TestData>
                .Create(context, d => d.Id)
                .FromEnumerable(input)
                .Transform(data => Task.FromResult(new ProcessedData(data.Id, data.Content.ToUpper())), "Process")
                    .WithArtifact(artifactNameFactory: d => $"{d.Id}_processed.json")
                .Action(_ => Task.CompletedTask, "DoNothing")
                .Complete();

            Assert.Equal(2, innerStorage.GetArtifactCount());
            var keys = innerStorage.GetAllKeys();
            Assert.Contains(keys, k => k.Contains("doc-1"));
            Assert.Contains(keys, k => k.Contains("doc-2"));
        }
    }

    /// <summary>
    /// Wrapper that adds delay to storage operations to simulate slow artifact saves
    /// </summary>
    private class SlowArtifactStorageService : IArtifactStorageService
    {
        private readonly IArtifactStorageService _inner;
        private readonly int _delayMs;

        public SlowArtifactStorageService(IArtifactStorageService inner, int delayMs)
        {
            _inner = inner;
            _delayMs = delayMs;
        }

        public async Task<List<ArtifactSaveResult>> SaveBatchAsync(List<ArtifactSaveRequest> requests, CancellationToken ct)
        {
            await Task.Delay(_delayMs, ct);
            return await _inner.SaveBatchAsync(requests, ct);
        }
    }
}

