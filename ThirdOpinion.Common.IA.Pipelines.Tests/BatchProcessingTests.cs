using System.Collections.Concurrent;
using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using ThirdOpinion.Common.IA.Pipelines.Core;
using Xunit;

namespace ThirdOpinion.Common.IA.Pipelines.Tests;

/// <summary>
/// Integration tests for batch processing functionality
/// </summary>
public class BatchProcessingTests
{
    private record TestData(string Id, int Value);
    private record ProcessedData(string Id, int Result);

    [Fact]
    public async Task Batch_ProcessesInCorrectSize()
    {
        // Arrange
        var input = Enumerable.Range(1, 25)
            .Select(i => new TestData($"item-{i}", i))
            .ToList();

        var context = new PipelineContext(
            Guid.NewGuid(),
            typeof(TestData),
            CancellationToken.None,
            NullLogger.Instance);

        var batchSizes = new ConcurrentBag<int>();

        // Act
        await DataFlowPipeline<TestData>
            .Create(context, PipelineSource<TestData>.FromEnumerable(input), d => d.Id)
            .Transform(data => Task.FromResult(new ProcessedData(data.Id, data.Value * 2)), "Process")
            .Batch(10)
            .Action(batch =>
            {
                batchSizes.Add(batch.Length);
                return Task.CompletedTask;
            }, "SaveBatch")
            .Complete();

        // Assert
        Assert.Equal(3, batchSizes.Count); // 3 batches total
        Assert.Equal(25, batchSizes.Sum());
        Assert.Equal(2, batchSizes.Count(size => size == 10));
        Assert.Equal(1, batchSizes.Count(size => size == 5));
        Assert.True(batchSizes.All(size => size is 10 or 5),
            $"Unexpected batch sizes encountered: [{string.Join(", ", batchSizes)}]");
    }

    [Fact]
    public async Task Batch_ProcessesAllItems()
    {
        // Arrange
        var input = Enumerable.Range(1, 50)
            .Select(i => new TestData($"item-{i}", i))
            .ToList();

        var context = new PipelineContext(
            Guid.NewGuid(),
            typeof(TestData),
            CancellationToken.None,
            NullLogger.Instance);

        var totalProcessed = 0;

        // Act
        await DataFlowPipeline<TestData>
            .Create(context, PipelineSource<TestData>.FromEnumerable(input), d => d.Id)
            .Transform(data => Task.FromResult(new ProcessedData(data.Id, data.Value * 2)), "Process")
            .Batch(7)
            .Action(batch =>
            {
                Interlocked.Add(ref totalProcessed, batch.Length);
                return Task.CompletedTask;
            }, "SaveBatch")
            .Complete();

        // Assert
        Assert.Equal(50, totalProcessed);
    }

    [Fact]
    public async Task Batch_WithErrors_ProcessesSuccessfulItems()
    {
        // Arrange
        var input = Enumerable.Range(1, 20)
            .Select(i => new TestData($"item-{i}", i))
            .ToList();

        var context = new PipelineContext(
            Guid.NewGuid(),
            typeof(TestData),
            CancellationToken.None,
            NullLogger.Instance);

        var successfulItems = new ConcurrentBag<ProcessedData>();

        // Act
        await DataFlowPipeline<TestData>
            .Create(context, PipelineSource<TestData>.FromEnumerable(input), d => d.Id)
            .Transform(data =>
            {
                if (data.Value % 2 == 0)
                    return Task.FromException<ProcessedData>(new InvalidOperationException("Even number"));

                return Task.FromResult(new ProcessedData(data.Id, data.Value * 2));
            }, "Process")
            .Batch(5)
            .Action(batch =>
            {
                foreach (var item in batch)
                {
                    successfulItems.Add(item);
                }
                return Task.CompletedTask;
            }, "SaveBatch")
            .Complete();

        // Assert
        Assert.Equal(10, successfulItems.Count); // Only odd numbers (1,3,5,7,9,11,13,15,17,19)
        Assert.All(successfulItems, item => Assert.True(item.Result % 4 == 2)); // Odd * 2
    }

    [Fact]
    public async Task Batch_SingleItem_CreatesOneBatch()
    {
        // Arrange
        var input = new[] { new TestData("single", 1) };

        var context = new PipelineContext(
            Guid.NewGuid(),
            typeof(TestData),
            CancellationToken.None,
            NullLogger.Instance);

        var batchCount = 0;

        // Act
        await DataFlowPipeline<TestData>
            .Create(context, PipelineSource<TestData>.FromEnumerable(input), d => d.Id)
            .Transform(data => Task.FromResult(new ProcessedData(data.Id, data.Value * 2)), "Process")
            .Batch(10)
            .Action(batch =>
            {
                batchCount++;
                Assert.Single(batch);
                return Task.CompletedTask;
            }, "SaveBatch")
            .Complete();

        // Assert
        Assert.Equal(1, batchCount);
    }

    [Fact]
    public async Task Batch_EmptyAfterErrors_CompletesSuccessfully()
    {
        // Arrange
        var input = new[] { new TestData("fail-1", 1), new TestData("fail-2", 2) };

        var context = new PipelineContext(
            Guid.NewGuid(),
            typeof(TestData),
            CancellationToken.None,
            NullLogger.Instance);

        var batchCount = 0;

        // Act
        await DataFlowPipeline<TestData>
            .Create(context, PipelineSource<TestData>.FromEnumerable(input), d => d.Id)
            .Transform<ProcessedData>(data =>
                Task.FromException<ProcessedData>(new InvalidOperationException("Always fails")), "Process")
            .Batch(10)
            .Action(batch =>
            {
                if (batch.Length > 0) // Only count non-empty batches
                    batchCount++;
                return Task.CompletedTask;
            }, "SaveBatch")
            .Complete();

        // Assert
        Assert.Equal(0, batchCount); // No batches if all items failed
    }

    [Fact]
    public async Task Batch_LargeBatchSize_ProcessesAllInOneBatch()
    {
        // Arrange
        var input = Enumerable.Range(1, 10)
            .Select(i => new TestData($"item-{i}", i))
            .ToList();

        var context = new PipelineContext(
            Guid.NewGuid(),
            typeof(TestData),
            CancellationToken.None,
            NullLogger.Instance);

        var batchCount = 0;
        var itemCount = 0;

        // Act
        await DataFlowPipeline<TestData>
            .Create(context, PipelineSource<TestData>.FromEnumerable(input), d => d.Id)
            .Transform(data => Task.FromResult(new ProcessedData(data.Id, data.Value * 2)), "Process")
            .Batch(100) // Larger than input
            .Action(batch =>
            {
                batchCount++;
                itemCount = batch.Length;
                return Task.CompletedTask;
            }, "SaveBatch")
            .Complete();

        // Assert
        Assert.Equal(1, batchCount);
        Assert.Equal(10, itemCount);
    }
}

