using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using ThirdOpinion.Common.IA.Pipelines.Core;
using ThirdOpinion.Common.IA.Pipelines.Services.InMemory;
using Xunit;
using static ThirdOpinion.Common.IA.Pipelines.Tests.TestTimings;

namespace ThirdOpinion.Common.IA.Pipelines.Tests;

/// <summary>
/// Integration tests for error handling and propagation
/// </summary>
public class ErrorHandlingTests
{
    private record TestData(string Id, int Value);
    private record ProcessedData(string Id, int Result);

    [Fact]
    public async Task ErrorInTransform_StopsProcessingForThatItem()
    {
        // Arrange
        var input = new[]
        {
            new TestData("success-1", 5),
            new TestData("fail-1", -1),  // Will fail
            new TestData("success-2", 10)
        };

        var context = new PipelineContext(
            Guid.NewGuid(),
            typeof(TestData),
            CancellationToken.None,
            NullLogger.Instance);

        var results = new ConcurrentBag<ProcessedData>();

        // Act
        await DataFlowPipeline<TestData>
            .Create(context, PipelineSource<TestData>.FromEnumerable(input), d => d.Id)
            .Transform(data =>
            {
                if (data.Value < 0)
                    return Task.FromException<ProcessedData>(new InvalidOperationException("Negative value"));

                return Task.FromResult(new ProcessedData(data.Id, data.Value * 2));
            }, "Process")
            .Action(result =>
            {
                results.Add(result);
                return Task.CompletedTask;
            }, "Collect")
            .Complete();

        // Assert
        Assert.Equal(2, results.Count); // Only successful items
        Assert.All(results, r => Assert.True(r.Result > 0));
        Assert.DoesNotContain(results, r => r.Id == "fail-1");
    }

    [Fact]
    public async Task ErrorInEarlyStep_SkipsDownstreamSteps()
    {
        // Arrange
        var input = new[] { new TestData("fail-1", -1) };
        var step2Executed = false;

        var context = new PipelineContext(
            Guid.NewGuid(),
            typeof(TestData),
            CancellationToken.None,
            NullLogger.Instance);

        // Act
        await DataFlowPipeline<TestData>
            .Create(context, PipelineSource<TestData>.FromEnumerable(input), d => d.Id)
            .Transform<ProcessedData>(data =>
                Task.FromException<ProcessedData>(new InvalidOperationException("Step 1 error")), "Step1")
            .Transform(data =>
            {
                step2Executed = true;
                return Task.FromResult(new ProcessedData(data.Id, data.Result * 2));
            }, "Step2")
            .Action(_ => Task.CompletedTask, "DoNothing")
            .Complete();

        // Assert
        Assert.False(step2Executed); // Step 2 should not execute
    }

    [Fact]
    public async Task MultipleErrors_AllAreTracked()
    {
        // Arrange
        var input = Enumerable.Range(1, 10)
            .Select(i => new TestData($"item-{i}", i))
            .ToList();

        var context = InMemoryServiceFactory.CreateContextWithProgress<TestData>(
            category: "Test",
            name: "ErrorHandling",
            cancellationToken: CancellationToken.None);
        
        var tracker = context.ProgressTracker as InMemoryProgressTracker;

        // Act
        await DataFlowPipeline<TestData>
            .Create(context, PipelineSource<TestData>.FromEnumerable(input), d => d.Id)
            .Transform(data =>
            {
                if (data.Value % 3 == 0)
                    return Task.FromException<ProcessedData>(new InvalidOperationException($"Error for {data.Id}"));

                return Task.FromResult(new ProcessedData(data.Id, data.Value * 2));
            }, "Process")
            .Action(_ => Task.CompletedTask, "DoNothing")
            .Complete();

        // Assert
        var snapshot = tracker.GetPipelineSnapshot();
        Assert.Equal(10, snapshot.TotalResources);
        Assert.Equal(7, snapshot.CompletedResources); // 1,2,4,5,7,8,10
        Assert.Equal(3, snapshot.FailedResources);    // 3,6,9
    }

    [Fact]
    public async Task ErrorInTerminalStep_IsHandledGracefully()
    {
        // Arrange
        var input = new[] { new TestData("test-1", 5) };
        var processedSuccessfully = false;

        var context = InMemoryServiceFactory.CreateContextWithProgress<TestData>(
            category: "Test",
            name: "ErrorHandling",
            cancellationToken: CancellationToken.None);
        
        var tracker = context.ProgressTracker as InMemoryProgressTracker;

        // Act
        await DataFlowPipeline<TestData>
            .Create(context, PipelineSource<TestData>.FromEnumerable(input), d => d.Id)
            .Transform(data =>
            {
                processedSuccessfully = true;
                return Task.FromResult(new ProcessedData(data.Id, data.Value * 2));
            }, "Process")
            .Action(result => Task.FromException(new InvalidOperationException("Terminal error")), "TerminalAction")
            .Complete();

        // Assert
        Assert.True(processedSuccessfully); // Processing succeeded
        var snapshot = tracker.GetPipelineSnapshot();
        Assert.Equal(1, snapshot.FailedResources); // Failed in terminal step
    }

    [Fact]
    public async Task CancellationToken_StopsProcessing()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var input = Enumerable.Range(1, 100)
            .Select(i => new TestData($"item-{i}", i))
            .ToList();

        var context = new PipelineContext(
            Guid.NewGuid(),
            typeof(TestData),
            cts.Token,
            NullLogger.Instance);

        var processedCount = 0;

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await DataFlowPipeline<TestData>
                .Create(context, PipelineSource<TestData>.FromEnumerable(input), d => d.Id)
                .Transform(async data =>
                {
                    processedCount++;
                    if (processedCount == 10)
                        cts.Cancel(); // Cancel after 10 items

                    await Task.Delay(SlowDelayMs, cts.Token);
                    return new ProcessedData(data.Id, data.Value * 2);
                }, "Process")
                .Action(_ => Task.CompletedTask, "DoNothing")
                .Complete();
        });

        Assert.True(processedCount < 100); // Should not process all items
    }
}

