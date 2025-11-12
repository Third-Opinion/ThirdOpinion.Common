using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using ThirdOpinion.Common.DataFlow.Core;
using Xunit;
using static ThirdOpinion.Common.DataFlow.Tests.TestTimings;

namespace ThirdOpinion.Common.DataFlow.Tests;

/// <summary>
/// Integration tests for simple pipeline execution without optional services
/// </summary>
public class SimplePipelineTests
{
    private record TestData(string Id, int Value);
    private record ProcessedData(string Id, int DoubledValue);
    private record EnrichedData(string Id, int DoubledValue, string Category);

    [Fact]
    public async Task SimplePipeline_ProcessesAllItems()
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

        var results = new ConcurrentBag<ProcessedData>();

        // Act
        await DataFlowPipeline<TestData>
            .Create(context, d => d.Id)
            .FromEnumerable(input)
            .Transform(async data =>
            {
                await Task.Delay(SlowDelayMs);
                return new ProcessedData(data.Id, data.Value * 2);
            }, "Process")
            .Action(result =>
            {
                results.Add(result);
                return Task.CompletedTask;
            }, "Collect")
            .Complete();

        // Assert
        var resultList = results.ToList();
        Assert.Equal(10, resultList.Count);
        Assert.All(resultList, r => Assert.True(r.DoubledValue > 0));
        Assert.Equal(2, resultList.First(r => r.Id == "item-1").DoubledValue);
        Assert.Equal(20, resultList.First(r => r.Id == "item-10").DoubledValue);
    }

    [Fact]
    public async Task MultipleTransforms_ChainsCorrectly()
    {
        // Arrange
        var input = new[] { new TestData("test-1", 5), new TestData("test-2", 15) };

        var context = new PipelineContext(
            Guid.NewGuid(),
            typeof(TestData),
            CancellationToken.None,
            NullLogger.Instance);

        var results = new ConcurrentBag<EnrichedData>();

        // Act
        await DataFlowPipeline<TestData>
            .Create(context, d => d.Id)
            .FromEnumerable(input)
            .Transform(data => Task.FromResult(new ProcessedData(data.Id, data.Value * 2)), "Double")
            .Transform(processed =>
            {
                var category = processed.DoubledValue < 20 ? "Low" : "High";
                return Task.FromResult(new EnrichedData(processed.Id, processed.DoubledValue, category));
            }, "Categorize")
            .Action(result =>
            {
                results.Add(result);
                return Task.CompletedTask;
            }, "Collect")
            .Complete();

        // Assert
        var resultList = results.ToList();
        Assert.Equal(2, resultList.Count);
        Assert.Equal("Low", resultList.First(r => r.Id == "test-1").Category);
        Assert.Equal("High", resultList.First(r => r.Id == "test-2").Category);
    }

    [Fact]
    public async Task FromAsyncSource_ProcessesStreamCorrectly()
    {
        // Arrange
        var context = new PipelineContext(
            Guid.NewGuid(),
            typeof(TestData),
            CancellationToken.None,
            NullLogger.Instance);

        var results = new ConcurrentBag<ProcessedData>();

        // Act
        await DataFlowPipeline<TestData>
            .Create(context, d => d.Id)
            .FromAsyncSource(GenerateDataAsync())
            .Transform(data => Task.FromResult(new ProcessedData(data.Id, data.Value * 2)), "Process")
            .Action(result =>
            {
                results.Add(result);
                return Task.CompletedTask;
            }, "Collect")
            .Complete();

        // Assert
        var resultList = results.ToList();
        Assert.Equal(5, resultList.Count);
        Assert.All(resultList, r => Assert.True(r.DoubledValue % 2 == 0));
    }

    [Fact]
    public async Task EmptySource_CompletesSuccessfully()
    {
        // Arrange
        var context = new PipelineContext(
            Guid.NewGuid(),
            typeof(TestData),
            CancellationToken.None,
            NullLogger.Instance);

        var results = new ConcurrentBag<ProcessedData>();

        // Act
        await DataFlowPipeline<TestData>
            .Create(context, d => d.Id)
            .FromEnumerable(Array.Empty<TestData>())
            .Transform(data => Task.FromResult(new ProcessedData(data.Id, data.Value * 2)), "Process")
            .Action(result =>
            {
                results.Add(result);
                return Task.CompletedTask;
            }, "Collect")
            .Complete();

        // Assert
        Assert.Empty(results);
    }

    private async IAsyncEnumerable<TestData> GenerateDataAsync()
    {
        for (int i = 1; i <= 5; i++)
        {
            await Task.Delay(MediumDelayMs);
            yield return new TestData($"async-{i}", i);
        }
    }
}

