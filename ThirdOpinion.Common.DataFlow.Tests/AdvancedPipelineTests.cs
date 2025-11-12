using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using ThirdOpinion.Common.DataFlow.Core;
using ThirdOpinion.Common.DataFlow.Models;
using ThirdOpinion.Common.DataFlow.Services.InMemory;
using Xunit.Abstractions;
using static ThirdOpinion.Common.DataFlow.Tests.TestTimings;

namespace ThirdOpinion.Common.DataFlow.Tests;

/// <summary>
/// Advanced integration tests demonstrating complex pipeline configurations
/// with bounded blocks, buffers, batching, parallel processing, and combined features.
/// These tests serve as documentation for advanced usage patterns.
/// </summary>
public class AdvancedPipelineTests
{
    private readonly ITestOutputHelper _output;

    public AdvancedPipelineTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private record DataItem(string Id, int Value, string Category);
    private record ProcessedItem(string Id, int ProcessedValue, string Category, DateTime ProcessedAt);
    private record EnrichedItem(string Id, int ProcessedValue, string Category, string Enrichment, DateTime ProcessedAt);
    private record PatientRecord(string ObservationId, string PatientId, int Value);
    private record PatientGroup(string PatientId, IReadOnlyList<PatientRecord> Observations);

    #region Bounded Capacity Tests

    [Fact]
    public async Task BoundedCapacity_AppliesBackpressure()
    {
        // Arrange - Test that bounded capacity prevents unbounded memory growth
        var itemCount = 1000;
        var input = Enumerable.Range(1, itemCount)
            .Select(i => new DataItem($"item-{i}", i, i % 3 == 0 ? "slow" : "fast"))
            .ToList();

        var context = new PipelineContext(
            Guid.NewGuid(),
            typeof(DataItem),
            CancellationToken.None,
            NullLogger.Instance);

        var results = new ConcurrentBag<ProcessedItem>();

        var boundedOptions = new PipelineStepOptions
        {
            MaxDegreeOfParallelism = 2,
            BoundedCapacity = 10  // Small buffer to force backpressure
        };

        // Act
        await DataFlowPipeline<DataItem>
            .Create(context, d => d.Id)
            .FromEnumerable(input)
            .Transform(async data =>
            {
                // Slow processing for some items
                if (data.Category == "slow")
                    await Task.Delay(VerySlowDelayMs);
                else
                    await Task.Delay(MinimalDelayMs);

                return new ProcessedItem(data.Id, data.Value * 2, data.Category, DateTime.UtcNow);
            }, "Process", boundedOptions)
            .Action(result =>
            {
                results.Add(result);
                return Task.CompletedTask;
            }, "Collect")
            .Complete();

        // Assert
        Assert.Equal(itemCount, results.Count);
        _output.WriteLine($"Processed {results.Count} items with bounded capacity of 10");
        _output.WriteLine($"Slow items: {results.Count(r => r.Category == "slow")}");
        _output.WriteLine($"Fast items: {results.Count(r => r.Category == "fast")}");
    }

    [Fact]
    public async Task BoundedCapacity_WithParallelProcessing()
    {
        // Arrange - Demonstrate bounded capacity with high parallelism
        var input = Enumerable.Range(1, 100)
            .Select(i => new DataItem($"item-{i}", i, "standard"))
            .ToList();

        var context = InMemoryServiceFactory.CreateContextWithProgress<DataItem>(
            category: "Test",
            name: "AdvancedPipeline",
            cancellationToken: CancellationToken.None);
        
        var tracker = context.ProgressTracker as InMemoryProgressTracker;

        var options = new PipelineStepOptions
        {
            MaxDegreeOfParallelism = 8,  // High parallelism
            BoundedCapacity = 20         // But limited buffer
        };

        var results = new ConcurrentBag<ProcessedItem>();

        // Act
        var startTime = DateTime.UtcNow;
        await DataFlowPipeline<DataItem>
            .Create(context, d => d.Id)
            .FromEnumerable(input)
            .Transform(async data =>
            {
                await Task.Delay(SlowDelayMs); // Simulate work
                return new ProcessedItem(data.Id, data.Value * 2, data.Category, DateTime.UtcNow);
            }, "Process", options)
            .Action(result =>
            {
                results.Add(result);
                return Task.CompletedTask;
            }, "Collect")
            .Complete();
        var endTime = DateTime.UtcNow;

        // Assert
        Assert.Equal(100, results.Count);
        var duration = (endTime - startTime).TotalMilliseconds;
        _output.WriteLine($"Processed 100 items in {duration}ms with 8 parallel workers and bounded capacity 20");
        
        var snapshot = tracker.GetPipelineSnapshot();
        Assert.Equal(100, snapshot.CompletedResources);
    }

    #endregion

    #region Complex Multi-Step Pipeline Tests

    [Fact]
    public async Task ComplexPipeline_MultiStepWithArtifactsAndBatching()
    {
        // Arrange - Demonstrate a realistic complex pipeline with multiple features
        var input = Enumerable.Range(1, 50)
            .Select(i => new DataItem($"item-{i}", i, i % 5 == 0 ? "premium" : "standard"))
            .ToList();

        var storage = new InMemoryArtifactStorageService();
        var batcherFactory = new InMemoryArtifactBatcherFactory(storage, batchSize: 5, flushIntervalMs: 100);
        
        var logger = NullLogger<AdvancedPipelineTests>.Instance;
        
        var contextBuilder = new PipelineContextBuilder(
            resourceType: typeof(DataItem),
            progressTrackerFactory: new InMemoryProgressTrackerFactory(),
            artifactBatcherFactory: batcherFactory,
            resourceRunCache: new InMemoryResourceRunCache(),
            logger: logger);
        
        var context = contextBuilder
            .WithCategory("Test")
            .WithName("AdvancedPipeline")
            .WithCancellationToken(CancellationToken.None)
            .Build();
        
        var tracker = context.ProgressTracker as InMemoryProgressTracker;

        var step1Options = new PipelineStepOptions { MaxDegreeOfParallelism = 4 };
        var step2Options = new PipelineStepOptions { MaxDegreeOfParallelism = 2, BoundedCapacity = 15 };

        var finalResults = new ConcurrentBag<EnrichedItem>();

        // Act - Complex pipeline with artifacts, multiple steps, and batching
        await DataFlowPipeline<DataItem>
            .Create(context, d => d.Id)
            .FromEnumerable(input)
            // Step 1: Process with high parallelism
            .Transform(async data =>
            {
                await Task.Delay(SlowDelayMs);
                return new ProcessedItem(
                    data.Id, 
                    data.Value * 2, 
                    data.Category,
                    DateTime.UtcNow);
            }, "Process", step1Options)
                .WithArtifact(
                    artifactNameFactory: p => $"{p.Id}_processed.json",
                    storageType: ArtifactStorageType.Memory)
            // Step 2: Enrich with bounded capacity
            .Transform(async processed =>
            {
                await Task.Delay(MediumDelayMs);
                var enrichment = processed.Category == "premium" ? "VIP" : "Regular";
                return new EnrichedItem(
                    processed.Id,
                    processed.ProcessedValue,
                    processed.Category,
                    enrichment,
                    processed.ProcessedAt);
            }, "Enrich", step2Options)
                .WithArtifact(
                    artifactNameFactory: e => $"{e.Id}_enriched.json",
                    storageType: ArtifactStorageType.Memory)
            // Step 3: Batch processing for efficient storage
            .Batch(10)
            .Action(async batch =>
            {
                // Simulate batch database insert
                await Task.Delay(VerySlowDelayMs);
                foreach (var item in batch)
                    finalResults.Add(item);
            }, "BatchSave")
            .Complete(batch => batch.Select(item => item.Id)); // Extract resource IDs from each item in batch

        // Assert
        Assert.Equal(50, finalResults.Count);
        Assert.Equal(10, finalResults.Count(r => r.Enrichment == "VIP"));
        Assert.Equal(40, finalResults.Count(r => r.Enrichment == "Regular"));
        foreach (var item in finalResults)
        {
            Assert.True(item.ProcessedValue >= 0);
            Assert.True(item.ProcessedAt > DateTime.MinValue);
        }
        
        // Verify artifacts were captured (2 per item)
        Assert.True(storage.GetArtifactCount() >= 90); // Should be ~100
        
        var snapshot = tracker.GetPipelineSnapshot();
        Assert.Equal(50, snapshot.CompletedResources);
        
        _output.WriteLine($"Complex pipeline completed:");
        _output.WriteLine($"  - Processed: {snapshot.CompletedResources} items");
        _output.WriteLine($"  - Artifacts captured: {storage.GetArtifactCount()}");
        _output.WriteLine($"  - Premium items: {finalResults.Count(r => r.Enrichment == "VIP")}");
    }

    [Fact]
    public async Task ComplexPipeline_ParallelProcessingWithDifferentCapacities()
    {
        // Arrange - Show how different steps can have different parallelism and capacity
        var input = Enumerable.Range(1, 100)
            .Select(i => new DataItem($"item-{i}", i, "standard"))
            .ToList();

        var context = new PipelineContext(
            Guid.NewGuid(),
            typeof(DataItem),
            CancellationToken.None,
            NullLogger.Instance);

        // Different options for each step
        var fastStepOptions = new PipelineStepOptions
        {
            MaxDegreeOfParallelism = 10,  // Fast step, high parallelism
            BoundedCapacity = 50
        };

        var slowStepOptions = new PipelineStepOptions
        {
            MaxDegreeOfParallelism = 2,   // Slow step, low parallelism
            BoundedCapacity = 10           // Small buffer
        };

        var results = new ConcurrentBag<EnrichedItem>();

        // Act
        await DataFlowPipeline<DataItem>
            .Create(context, d => d.Id)
            .FromEnumerable(input)
            // Fast step - quick processing, high throughput
            .Transform(async data =>
            {
                await Task.Delay(MediumDelayMs);
                return new ProcessedItem(data.Id, data.Value * 2, data.Category, DateTime.UtcNow);
            }, "FastProcess", fastStepOptions)
            // Slow step - intensive processing, limited parallelism
            .Transform(async processed =>
            {
                await Task.Delay(VerySlowDelayMs);
                return new EnrichedItem(
                    processed.Id,
                    processed.ProcessedValue,
                    processed.Category,
                    "Enhanced",
                    processed.ProcessedAt);
            }, "SlowEnrich", slowStepOptions)
            .Action(result =>
            {
                results.Add(result);
                return Task.CompletedTask;
            }, "Collect")
            .Complete();

        // Assert
        Assert.Equal(100, results.Count);
        _output.WriteLine("Pipeline with mixed step configurations:");
        _output.WriteLine($"  - Fast step: {fastStepOptions.MaxDegreeOfParallelism} workers, capacity {fastStepOptions.BoundedCapacity}");
        _output.WriteLine($"  - Slow step: {slowStepOptions.MaxDegreeOfParallelism} workers, capacity {slowStepOptions.BoundedCapacity}");
        _output.WriteLine($"  - Results: {results.Count}");
    }

    #endregion

    #region Sequential Grouping Tests

    [Fact]
    public async Task GroupSequential_GroupsOrderedItemsIntoBatches()
    {
        // Arrange
        var orderedRecords = new List<PatientRecord>
        {
            new("obs-1", "patient-1", 10),
            new("obs-2", "patient-1", 15),
            new("obs-3", "patient-2", 20),
            new("obs-4", "patient-3", 5),
            new("obs-5", "patient-3", 7),
            new("obs-6", "patient-3", 9)
        };

        var context = new PipelineContext(
            Guid.NewGuid(),
            typeof(PatientRecord),
            CancellationToken.None,
            NullLogger.Instance);

        var groupedPatients = new ConcurrentQueue<PatientGroup>();

        // Act
        await DataFlowPipeline<PatientRecord>
            .Create(context, record => record.PatientId)
            .FromEnumerable(orderedRecords)
            .GroupSequential(
                record => record.PatientId,
                (patientId, records) => new PatientGroup(patientId, records.ToList()),
                patientId => patientId,
                "GroupByPatient")
            .Action(group =>
            {
                groupedPatients.Enqueue(group);
                return Task.CompletedTask;
            }, "Collect")
            .Complete();

        // Assert
        var groupedPatientsSnapshot = groupedPatients.ToArray();
        Assert.Equal(3, groupedPatientsSnapshot.Length);

        var orderedGroups = groupedPatientsSnapshot
            .OrderBy(group => group.PatientId)
            .ToList();

        var first = orderedGroups[0];
        Assert.Equal("patient-1", first.PatientId);
        Assert.Equal(2, first.Observations.Count);

        var second = orderedGroups[1];
        Assert.Equal("patient-2", second.PatientId);
        Assert.Single(second.Observations);

        var third = orderedGroups[2];
        Assert.Equal("patient-3", third.PatientId);
        Assert.Equal(3, third.Observations.Count);

        var allObservationIds = groupedPatientsSnapshot
            .SelectMany(group => group.Observations)
            .Select(record => record.ObservationId)
            .ToList();
        Assert.All(allObservationIds, id => Assert.False(string.IsNullOrWhiteSpace(id)));

        Assert.All(groupedPatientsSnapshot, group =>
            Assert.All(group.Observations, record => Assert.Equal(group.PatientId, record.PatientId)));
    }

    [Fact]
    public async Task GroupSequential_FromAsyncSource_GroupsStreamingData()
    {
        // Arrange
        var orderedRecords = new List<PatientRecord>
        {
            new("obs-1", "patient-1", 12),
            new("obs-2", "patient-1", 18),
            new("obs-3", "patient-2", 7),
            new("obs-4", "patient-2", 11),
            new("obs-5", "patient-3", 25)
        };

        async IAsyncEnumerable<PatientRecord> StreamRecords()
        {
            foreach (var record in orderedRecords)
            {
                await Task.Delay(MediumDelayMs);
                yield return record;
            }
        }

        var context = new PipelineContext(
            Guid.NewGuid(),
            typeof(PatientRecord),
            CancellationToken.None,
            NullLogger.Instance);

        var groups = new ConcurrentQueue<PatientGroup>();

        // Act
        await DataFlowPipeline<PatientRecord>
            .Create(context, record => record.PatientId)
            .FromAsyncSource(StreamRecords())
            .GroupSequential(
                record => record.PatientId,
                (patientId, records) => new PatientGroup(patientId, records.ToList()),
                patientId => patientId,
                "StreamGroup")
            .Action(group =>
            {
                groups.Enqueue(group);
                return Task.CompletedTask;
            }, "Collect")
            .Complete();

        // Assert
        var groupsSnapshot = groups.ToArray();
        Assert.Equal(new[] { "patient-1", "patient-2", "patient-3" }, groupsSnapshot.Select(g => g.PatientId));
        Assert.Equal(2, groupsSnapshot[0].Observations.Count);
        Assert.Equal(2, groupsSnapshot[1].Observations.Count);
        Assert.Single(groupsSnapshot[2].Observations);
    }

    [Fact]
    public async Task GroupSequential_ChainedInPipelineStepBuilder()
    {
        // Arrange
        var dataItems = Enumerable.Range(1, 6)
            .Select(i => new DataItem($"item-{i}", i * 10, i <= 2 ? "patient-1" : i <= 4 ? "patient-2" : "patient-3"))
            .ToList();

        var context = new PipelineContext(
            Guid.NewGuid(),
            typeof(DataItem),
            CancellationToken.None,
            NullLogger.Instance);

        var summaries = new ConcurrentBag<(string PatientId, int ObservationCount, int TotalValue)>();

        // Act
        await DataFlowPipeline<DataItem>
            .Create(context, item => item.Id)
            .FromEnumerable(dataItems)
            .Transform(item => new PatientRecord($"{item.Id}-obs", item.Category, item.Value), "ToPatientRecord")
            .GroupSequential(
                record => record.PatientId,
                (patientId, records) => new PatientGroup(patientId, records.ToList()),
                patientId => patientId,
                "GroupAfterTransform")
            .Action(group =>
            {
                var total = group.Observations.Sum(obs => obs.Value);
                summaries.Add((group.PatientId, group.Observations.Count, total));
                return Task.CompletedTask;
            }, "SummarizeGroups")
            .Complete();

        // Assert
        var summarySnapshot = summaries.ToArray();
        Assert.Equal(3, summarySnapshot.Length);
        Assert.Contains(("patient-1", 2, 30), summarySnapshot);
        Assert.Contains(("patient-2", 2, 70), summarySnapshot);
        Assert.Contains(("patient-3", 2, 110), summarySnapshot);
    }

    #endregion

    #region TransformMany with Batching Tests

    [Fact]
    public async Task ComplexPipeline_TransformManyWithBatching()
    {
        // Arrange - Demonstrate one-to-many expansion followed by batch processing
        var parentItems = Enumerable.Range(1, 10)
            .Select(i => new DataItem($"parent-{i}", i, "batch"))
            .ToList();

        var context = new PipelineContext(
            Guid.NewGuid(),
            typeof(DataItem),
            CancellationToken.None,
            NullLogger.Instance);

        var expandedCount = 0;
        var batchSizes = new ConcurrentQueue<int>();

        // Act
        await DataFlowPipeline<DataItem>
            .Create(context, d => d.Id)
            .FromEnumerable(parentItems)
            // Expand each parent into multiple children
            .TransformMany<ProcessedItem>(
                async parent =>
                {
                    await Task.Delay(MediumDelayMs);
                    // Each parent generates 5 children
                    return Enumerable.Range(1, 5)
                        .Select(i => new ProcessedItem(
                            $"{parent.Id}-child-{i}",
                            parent.Value * 10 + i,
                            parent.Category,
                            DateTime.UtcNow))
                        .ToList();
                },
                child => child.Id,
                "ExpandToChildren")
            // Further process each child
            .Transform(async child =>
            {
                await Task.Delay(FastDelayMs);
                Interlocked.Increment(ref expandedCount);
                return new EnrichedItem(
                    child.Id,
                    child.ProcessedValue,
                    child.Category,
                    "ChildProcessed",
                    child.ProcessedAt);
            }, "ProcessChild")
            // Batch for efficient storage
            .Batch(15)
            .Action(async batch =>
            {
                batchSizes.Enqueue(batch.Length);
                await Task.Delay(SlowDelayMs);
            }, "SaveBatch")
            .Complete();

        // Assert
        Assert.Equal(50, expandedCount); // 10 parents * 5 children each
        var batchArray = batchSizes.ToArray();
        Assert.Equal(4, batchArray.Length); // 50 items / 15 per batch = 4 batches (15,15,15,5) in any order
        Assert.Equal(50, batchArray.Sum());
        Assert.Equal(3, batchArray.Count(size => size == 15));
        Assert.Equal(1, batchArray.Count(size => size == 5));
        Assert.True(batchArray.All(size => size is 5 or 15),
            $"Unexpected batch sizes encountered: [{string.Join(", ", batchArray)}]");

        _output.WriteLine($"TransformMany pipeline:");
        _output.WriteLine($"  - Parents: 10");
        _output.WriteLine($"  - Children per parent: 5");
        _output.WriteLine($"  - Total children: {expandedCount}");
        _output.WriteLine($"  - Batches: {batchSizes.Count}");
        _output.WriteLine($"  - Batch sizes: [{string.Join(", ", batchSizes)}]");
    }

    [Fact]
    public async Task ComplexPipeline_TransformManyWithArtifacts()
    {
        // Arrange - Show artifacts captured during TransformMany operations
        var parentItems = new[]
        {
            new DataItem("parent-1", 100, "type-a"),
            new DataItem("parent-2", 200, "type-b")
        };

        var storage = new InMemoryArtifactStorageService();
        var batcherFactory = new InMemoryArtifactBatcherFactory(storage, batchSize: 5, flushIntervalMs: 100);
        
        var builder = new PipelineContextBuilder(
            resourceType: typeof(DataItem),
            progressTrackerFactory: null,
            artifactBatcherFactory: batcherFactory,
            resourceRunCache: new InMemoryResourceRunCache(),
            logger: NullLogger.Instance);
        
        var context = builder
            .WithCategory("Test")
            .WithName("AdvancedPipeline")
            .WithCancellationToken(CancellationToken.None)
            .Build();

        var results = new ConcurrentBag<EnrichedItem>();

        // Act
        await DataFlowPipeline<DataItem>
            .Create(context, d => d.Id)
            .FromEnumerable(parentItems)
            // Capture parent artifact before expansion
            .Transform(async data =>
            {
                await Task.Delay(MediumDelayMs);
                return new ProcessedItem(data.Id, data.Value, data.Category, DateTime.UtcNow);
            }, "ProcessParent")
                .WithArtifact(
                    artifactNameFactory: p => $"{p.Id}_parent.json",
                    storageType: ArtifactStorageType.Memory)
            // Expand to children
            .TransformMany<ProcessedItem>(
                parent => Task.FromResult<IEnumerable<ProcessedItem>>(
                    Enumerable.Range(1, 3)
                        .Select(i => new ProcessedItem(
                            $"{parent.Id}-child-{i}",
                            parent.ProcessedValue + i,
                            parent.Category,
                            DateTime.UtcNow))
                        .ToList()),
                child => child.Id,
                "ExpandToChildren")
            // Process and capture each child
            .Transform(async child =>
            {
                await Task.Delay(FastDelayMs);
                return new EnrichedItem(
                    child.Id,
                    child.ProcessedValue * 2,
                    child.Category,
                    "Processed",
                    child.ProcessedAt);
            }, "ProcessChild")
                .WithArtifact(
                    artifactNameFactory: e => $"{e.Id}_child.json",
                    storageType: ArtifactStorageType.Memory)
            .Action(result =>
            {
                results.Add(result);
                return Task.CompletedTask;
            }, "Collect")
            .Complete();

        await Task.Delay(ArtifactFlushBufferMs); // Let artifacts flush

        // Assert
        Assert.Equal(6, results.Count); // 2 parents * 3 children
        Assert.All(results, r => Assert.True(r.ProcessedAt > DateTime.MinValue));
        
        // Should have artifacts for parents and all children
        Assert.True(storage.GetArtifactCount() >= 7); // 2 parents + 6 children = 8 total
        
        _output.WriteLine($"TransformMany with artifacts:");
        _output.WriteLine($"  - Parent artifacts: 2");
        _output.WriteLine($"  - Child results: {results.Count}");
        _output.WriteLine($"  - Total artifacts: {storage.GetArtifactCount()}");
    }

    #endregion

    #region High-Throughput Stress Tests

    [Fact]
    public async Task HighThroughput_LargeDatasetWithParallelProcessing()
    {
        // Arrange - Stress test with large dataset
        var itemCount = 1000;
        var input = Enumerable.Range(1, itemCount)
            .Select(i => new DataItem($"item-{i}", i, $"category-{i % 10}"))
            .ToList();

        var context = InMemoryServiceFactory.CreateContextWithProgress<DataItem>(
            category: "Test",
            name: "AdvancedPipeline",
            cancellationToken: CancellationToken.None);
        
        var tracker = context.ProgressTracker as InMemoryProgressTracker;

        var options = new PipelineStepOptions
        {
            MaxDegreeOfParallelism = 10,
            BoundedCapacity = 100
        };

        var results = new ConcurrentBag<ProcessedItem>();

        // Act
        var startTime = DateTime.UtcNow;
        await DataFlowPipeline<DataItem>
            .Create(context, d => d.Id)
            .FromEnumerable(input)
            .Transform(async data =>
            {
                await Task.Delay(MinimalDelayMs); // Minimal delay
                return new ProcessedItem(data.Id, data.Value * 2, data.Category, DateTime.UtcNow);
            }, "Process", options)
            .Batch(50)
            .Action(batch =>
            {
                foreach (var item in batch)
                    results.Add(item);
                return Task.CompletedTask;
            }, "BatchCollect")
            .Complete(batch => batch.Select(item => item.Id)); // Extract resource IDs from each item in batch
        var endTime = DateTime.UtcNow;

        // Assert
        Assert.Equal(itemCount, results.Count);
        
        var duration = (endTime - startTime).TotalMilliseconds;
        var throughput = itemCount / (duration / 1000.0);
        
        var snapshot = tracker.GetPipelineSnapshot();
        Assert.Equal(itemCount, snapshot.CompletedResources);
        
        _output.WriteLine($"High-throughput pipeline:");
        _output.WriteLine($"  - Items: {itemCount}");
        _output.WriteLine($"  - Duration: {duration:F0}ms");
        _output.WriteLine($"  - Throughput: {throughput:F0} items/sec");
        _output.WriteLine($"  - Parallelism: {options.MaxDegreeOfParallelism}");
        _output.WriteLine($"  - Batch size: 50");
    }

    [Fact]
    public async Task HighThroughput_WithFullFeatureSet()
    {
        // Arrange - Kitchen sink test with all features
        var itemCount = 500;
        var input = Enumerable.Range(1, itemCount)
            .Select(i => new DataItem($"item-{i}", i, i % 2 == 0 ? "even" : "odd"))
            .ToList();

        var storage = new InMemoryArtifactStorageService();
        var batcherFactory = new InMemoryArtifactBatcherFactory(storage, batchSize: 20, flushIntervalMs: 50);
        var tracker = new InMemoryProgressTracker();
        
        var contextBuilder = new PipelineContextBuilder(
            resourceType: typeof(DataItem),
            progressTrackerFactory: new InMemoryProgressTrackerFactory(),
            artifactBatcherFactory: batcherFactory,
            resourceRunCache: new InMemoryResourceRunCache(),
            logger: NullLogger.Instance);
        
        var context = contextBuilder
            .WithCategory("Test")
            .WithName("AdvancedPipeline")
            .WithCancellationToken(CancellationToken.None)
            .Build();

        var step1Options = new PipelineStepOptions
        {
            MaxDegreeOfParallelism = 8,
            BoundedCapacity = 50
        };

        var step2Options = new PipelineStepOptions
        {
            MaxDegreeOfParallelism = 4,
            BoundedCapacity = 30
        };

        var results = new ConcurrentBag<EnrichedItem>();

        // Act - Full-featured pipeline
        var startTime = DateTime.UtcNow;
        await DataFlowPipeline<DataItem>
            .Create(context, d => d.Id)
            .FromEnumerable(input)
            .Transform(async data =>
            {
                await Task.Delay(FastDelayMs);
                return new ProcessedItem(data.Id, data.Value * 2, data.Category, DateTime.UtcNow);
            }, "Process", step1Options)
                .WithArtifact(
                    artifactNameFactory: p => $"step1/{p.Category}/{p.Id}.json",
                    storageType: ArtifactStorageType.Memory)
            .Transform(async processed =>
            {
                await Task.Delay(MinimalDelayMs);
                return new EnrichedItem(
                    processed.Id,
                    processed.ProcessedValue,
                    processed.Category,
                    "Enhanced",
                    processed.ProcessedAt);
            }, "Enrich", step2Options)
                .WithArtifact(
                    artifactNameFactory: e => $"step2/{e.Category}/{e.Id}.json",
                    storageType: ArtifactStorageType.Memory)
            .Batch(25)
            .Action(batch =>
            {
                foreach (var item in batch)
                    results.Add(item);
                return Task.CompletedTask;
            }, "BatchSave")
            .Complete();
        var endTime = DateTime.UtcNow;

        // Let artifacts flush and batches complete
        await Task.Delay(FullFeatureDrainBufferMs); // Allow batch completion and artifact flushing

        // Assert
        Assert.Equal(itemCount, results.Count);
        Assert.Equal(250, results.Count(r => r.Category == "even"));
        Assert.Equal(250, results.Count(r => r.Category == "odd"));
        Assert.All(results, item => Assert.True(item.ProcessedAt > DateTime.MinValue));
        
        var duration = (endTime - startTime).TotalMilliseconds;
        var snapshot = tracker.GetPipelineSnapshot();
        
        _output.WriteLine($"Full-featured high-throughput pipeline:");
        _output.WriteLine($"  - Items: {itemCount}");
        _output.WriteLine($"  - Duration: {duration:F0}ms");
        _output.WriteLine($"  - Throughput: {itemCount / (duration / 1000.0):F0} items/sec");
        _output.WriteLine($"  - Completed: {snapshot.CompletedResources}");
        _output.WriteLine($"  - Failed: {snapshot.FailedResources}");
        _output.WriteLine($"  - Artifacts: {storage.GetArtifactCount()}");
        _output.WriteLine($"  - Step 1: {step1Options.MaxDegreeOfParallelism} workers, capacity {step1Options.BoundedCapacity}");
        _output.WriteLine($"  - Step 2: {step2Options.MaxDegreeOfParallelism} workers, capacity {step2Options.BoundedCapacity}");
    }

    #endregion
}

