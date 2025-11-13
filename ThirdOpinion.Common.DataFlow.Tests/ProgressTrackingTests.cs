using ThirdOpinion.Common.DataFlow.Core;
using ThirdOpinion.Common.DataFlow.Models;
using ThirdOpinion.Common.DataFlow.Services.InMemory;

namespace ThirdOpinion.Common.DataFlow.Tests;

/// <summary>
/// Integration tests for progress tracking functionality
/// </summary>
public class ProgressTrackingTests
{
    private record TestData(string Id, int Value);
    private record ProcessedData(string Id, int Result);

    [Fact]
    public async Task ProgressTracker_RecordsAllResources()
    {
        // Arrange
        var input = Enumerable.Range(1, 10)
            .Select(i => new TestData($"item-{i}", i))
            .ToList();

        var context = InMemoryServiceFactory.CreateContextWithProgress<TestData>(
            category: "Test",
            name: "ProgressTracking",
            cancellationToken: CancellationToken.None);
        
        var tracker = context.ProgressTracker as InMemoryProgressTracker;

        // Act
        await DataFlowPipeline<TestData>
            .Create(context, PipelineSource<TestData>.FromEnumerable(input), d => d.Id)
            .Transform(data => Task.FromResult(new ProcessedData(data.Id, data.Value * 2)), "Process")
            .Action(_ => Task.CompletedTask, "DoNothing")
            .Complete();

        // Assert
        var snapshot = tracker.GetPipelineSnapshot();
        Assert.Equal(10, snapshot.TotalResources);
        Assert.Equal(10, snapshot.CompletedResources);
        Assert.Equal(0, snapshot.FailedResources);
        Assert.Equal(0, snapshot.ProcessingResources);
    }

    [Fact]
    public async Task ProgressTracker_RecordsStepMetrics()
    {
        // Arrange
        var input = new[] { new TestData("test-1", 5) };
        var context = InMemoryServiceFactory.CreateContextWithProgress<TestData>(
            category: "Test",
            name: "ProgressTracking",
            cancellationToken: CancellationToken.None);
        
        var tracker = context.ProgressTracker as InMemoryProgressTracker;

        // Act
        await DataFlowPipeline<TestData>
            .Create(context, PipelineSource<TestData>.FromEnumerable(input), d => d.Id)
            .Transform(data => Task.FromResult(new ProcessedData(data.Id, data.Value * 2)), "Process")
            .Action(_ => Task.CompletedTask, "DoNothing")
            .Complete();

        // Assert
        var states = tracker.GetAllResourceStates();
        Assert.Single(states);
        
        var state = states["test-1"];
        Assert.Equal(PipelineResourceStatus.Completed, state.Status);
        Assert.Contains(state.StepProgresses, s => s.StepName == "Process");
        Assert.Contains(state.StepProgresses, s => s.StepName == "DoNothing");
        var processStep = state.StepProgresses.First(s => s.StepName == "Process");
        Assert.Equal(PipelineStepStatus.Completed, processStep.Status);
    }

    [Fact]
    public async Task ProgressTracker_RecordsFailures()
    {
        // Arrange
        var input = new[] { new TestData("fail-1", 5) };
        var context = InMemoryServiceFactory.CreateContextWithProgress<TestData>(
            category: "Test",
            name: "ProgressTracking",
            cancellationToken: CancellationToken.None);
        
        var tracker = context.ProgressTracker as InMemoryProgressTracker;

        // Act
        await DataFlowPipeline<TestData>
            .Create(context, PipelineSource<TestData>.FromEnumerable(input), d => d.Id)
            .Transform<ProcessedData>(data =>
                Task.FromException<ProcessedData>(new InvalidOperationException("Test error")), "Process")
            .Action(_ => Task.CompletedTask, "DoNothing")
            .Complete();

        // Assert
        var snapshot = tracker.GetPipelineSnapshot();
        Assert.Equal(1, snapshot.FailedResources);
        
        var states = tracker.GetAllResourceStates();
        var state = states["fail-1"];
        Assert.Equal(PipelineResourceStatus.Failed, state.Status);
        Assert.NotNull(state.ErrorMessage);
        Assert.Contains("Test error", state.ErrorMessage);
    }

    [Fact]
    public async Task ProgressTracker_HandlesMixedSuccessAndFailure()
    {
        // Arrange
        var input = Enumerable.Range(1, 10)
            .Select(i => new TestData($"item-{i}", i))
            .ToList();

        var context = InMemoryServiceFactory.CreateContextWithProgress<TestData>(
            category: "Test",
            name: "ProgressTracking",
            cancellationToken: CancellationToken.None);
        
        var tracker = context.ProgressTracker as InMemoryProgressTracker;

        // Act
        await DataFlowPipeline<TestData>
            .Create(context, PipelineSource<TestData>.FromEnumerable(input), d => d.Id)
            .Transform(data =>
            {
                if (data.Value % 2 == 0)
                    return Task.FromException<ProcessedData>(new InvalidOperationException("Even number error"));
                
                return Task.FromResult(new ProcessedData(data.Id, data.Value * 2));
            }, "Process")
            .Action(_ => Task.CompletedTask, "DoNothing")
            .Complete();

        // Assert
        var snapshot = tracker.GetPipelineSnapshot();
        Assert.Equal(10, snapshot.TotalResources);
        Assert.Equal(5, snapshot.CompletedResources); // Odd numbers
        Assert.Equal(5, snapshot.FailedResources);    // Even numbers
    }
}

