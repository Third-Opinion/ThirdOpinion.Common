using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using ThirdOpinion.Common.IA.Pipelines.Core;
using ThirdOpinion.Common.IA.Pipelines.Models;
using ThirdOpinion.Common.IA.Pipelines.Services.InMemory;
using Xunit;
using static ThirdOpinion.Common.IA.Pipelines.Tests.TestTimings;

namespace ThirdOpinion.Common.IA.Pipelines.Tests;

/// <summary>
/// Integration tests for TransformMany operations
/// </summary>
public class TransformManyTests
{
    private record ParentData(string Id, string[] Children);
    private record ChildData(string ParentId, string ChildId, int Value);
    private record ProcessedChild(string ParentId, string ChildId, int ProcessedValue);
    private record AggregatedParent(string ParentId, IReadOnlyList<ChildData> Children);

    [Fact]
    public async Task TransformMany_ExpandsCorrectly()
    {
        // Arrange
        var input = new[]
        {
            new ParentData("parent-1", new[] { "child-1", "child-2", "child-3" }),
            new ParentData("parent-2", new[] { "child-4", "child-5" })
        };

        var context = new PipelineContext(
            Guid.NewGuid(),
            typeof(ParentData),
            CancellationToken.None,
            NullLogger.Instance);

        var results = new ConcurrentBag<ChildData>();

        // Act
        await DataFlowPipeline<ParentData>
            .Create(context, PipelineSource<ParentData>.FromEnumerable(input), d => d.Id)
            .TransformMany(
                parent => Task.FromResult<IEnumerable<ChildData>>(
                    parent.Children.Select((child, idx) =>
                        new ChildData(parent.Id, child, idx + 1)).ToList()),
                child => child.ChildId,
                "ExpandChildren")
            .Action(child =>
            {
                results.Add(child);
                return Task.CompletedTask;
            }, "Collect")
            .Complete();

        // Assert
        var resultList = results.ToList();
        Assert.Equal(5, resultList.Count); // 3 + 2 children
        Assert.Equal(3, resultList.Count(r => r.ParentId == "parent-1"));
        Assert.Equal(2, resultList.Count(r => r.ParentId == "parent-2"));
    }

    [Fact]
    public async Task TransformMany_TracksAllChildren()
    {
        // Arrange
        var input = new[] { new ParentData("parent-1", new[] { "child-1", "child-2" }) };

        var context = InMemoryServiceFactory.CreateContextWithProgress<ParentData>(
            category: "Test",
            name: "TransformMany",
            cancellationToken: CancellationToken.None);
        
        var tracker = context.ProgressTracker as InMemoryProgressTracker;

        // Act
        await DataFlowPipeline<ParentData>
            .Create(context, PipelineSource<ParentData>.FromEnumerable(input), d => d.Id)
            .TransformMany(
                parent => Task.FromResult<IEnumerable<ChildData>>(
                    parent.Children.Select(child =>
                        new ChildData(parent.Id, child, 1)).ToList()),
                child => child.ChildId,
                "ExpandChildren")
            .Action(_ => Task.CompletedTask, "DoNothing")
            .Complete();

        // Assert
        var states = tracker.GetAllResourceStates();
        var state = Assert.Single(states);
        Assert.Equal("parent-1", state.Key);
        Assert.NotEmpty(state.Value.StepProgresses);
    }

    [Fact]
    public async Task TransformMany_WithDownstreamProcessing()
    {
        // Arrange
        var input = new[]
        {
            new ParentData("parent-1", new[] { "child-1", "child-2" })
        };

        var context = new PipelineContext(
            Guid.NewGuid(),
            typeof(ParentData),
            CancellationToken.None,
            NullLogger.Instance);

        var results = new ConcurrentBag<ProcessedChild>();

        // Act
        await DataFlowPipeline<ParentData>
            .Create(context, PipelineSource<ParentData>.FromEnumerable(input), d => d.Id)
            .TransformMany(
                parent => Task.FromResult<IEnumerable<ChildData>>(
                    parent.Children.Select((child, idx) =>
                        new ChildData(parent.Id, child, idx + 1)).ToList()),
                child => child.ChildId,
                "ExpandChildren")
            .Transform(async child =>
            {
                await Task.Delay(SlowDelayMs);
                return new ProcessedChild(child.ParentId, child.ChildId, child.Value * 10);
            }, "ProcessChild")
            .Action(processed =>
            {
                results.Add(processed);
                return Task.CompletedTask;
            }, "Collect")
            .Complete();

        // Assert
        var processedList = results.ToList();
        Assert.Equal(2, processedList.Count);
        Assert.Contains(processedList, r => r.ChildId == "child-1" && r.ProcessedValue == 10);
        Assert.Contains(processedList, r => r.ChildId == "child-2" && r.ProcessedValue == 20);
    }

    [Fact]
    public async Task TransformMany_EmptyExpansion_CompletesSuccessfully()
    {
        // Arrange
        var input = new[] { new ParentData("parent-1", Array.Empty<string>()) };

        var context = new PipelineContext(
            Guid.NewGuid(),
            typeof(ParentData),
            CancellationToken.None,
            NullLogger.Instance);

        var results = new ConcurrentBag<ChildData>();

        // Act
        await DataFlowPipeline<ParentData>
            .Create(context, PipelineSource<ParentData>.FromEnumerable(input), d => d.Id)
            .TransformMany(
                parent => Task.FromResult<IEnumerable<ChildData>>(
                    parent.Children.Select(child =>
                        new ChildData(parent.Id, child, 1)).ToList()),
                child => child.ChildId,
                "ExpandChildren")
            .Action(child =>
            {
                results.Add(child);
                return Task.CompletedTask;
            }, "Collect")
            .Complete();

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task TransformMany_DoesNotDoubleCountResources()
    {
        // Arrange
        const int resourceCount = 100;
        var input = Enumerable.Range(1, resourceCount)
            .Select(i => new ParentData($"parent-{i:D3}", new[] { $"child-{i:D3}-1", $"child-{i:D3}-2" }))
            .ToArray();

        var context = InMemoryServiceFactory.CreateContextWithProgress<ParentData>(
            category: "Test",
            name: "TransformManyDoubleCount",
            cancellationToken: CancellationToken.None);

        var tracker = (InMemoryProgressTracker)context.ProgressTracker!;

        // Act
        await DataFlowPipeline<ParentData>
            .Create(context, PipelineSource<ParentData>.FromEnumerable(input), d => d.Id)
            .TransformMany(
                parent => Task.FromResult<IEnumerable<ChildData>>(
                    parent.Children.Select(child => new ChildData(parent.Id, child, 1)).ToList()),
                child => child.ChildId,
                "ExpandChildren")
            .Transform(child => Task.FromResult(child), "Identity")
            .Action(_ => Task.CompletedTask, "Collect")
            .Complete();

        // Assert
        // We expect each parent resource to appear once in the tracker
        var states = tracker.GetAllResourceStates();
        Assert.Equal(resourceCount, states.Count);

        foreach (var parent in input)
        {
            Assert.True(states.ContainsKey(parent.Id));
        }
    }

    [Fact]
    public async Task TransformMany_WithGrouping_DoesNotDoubleCountResources()
    {
        // Arrange
        const int resourceCount = 100;
        var input = Enumerable.Range(1, resourceCount)
            .Select(i => new ParentData($"parent-{i:D3}", new[] { $"child-{i:D3}-1", $"child-{i:D3}-2" }))
            .ToArray();

        var context = InMemoryServiceFactory.CreateContextWithProgress<ParentData>(
            category: "Test",
            name: "TransformManyGrouping",
            cancellationToken: CancellationToken.None);

        var tracker = (InMemoryProgressTracker)context.ProgressTracker!;

        var aggregated = new ConcurrentBag<AggregatedParent>();

        // Act
        await DataFlowPipeline<ParentData>
            .Create(context, PipelineSource<ParentData>.FromEnumerable(input), d => d.Id)
            .TransformMany(
                parent => Task.FromResult<IEnumerable<ChildData>>(
                    parent.Children.Select(child => new ChildData(parent.Id, child, 1)).ToList()),
                child => child.ChildId,
                "ExpandChildren")
            .GroupSequential(
                child => child.ParentId,
                (parentId, children) => new AggregatedParent(parentId, children.ToList()),
                parentId => parentId,
                "GroupByParent")
            .Action(result =>
            {
                aggregated.Add(result);
                return Task.CompletedTask;
            }, "PersistAggregated")
            .Complete(result => new[] { result.ParentId });

        // Assert
        Assert.Equal(resourceCount, aggregated.Count);

        var states = tracker.GetAllResourceStates();
        Assert.Equal(resourceCount, states.Count);
        Assert.All(states.Values, state => Assert.Equal(PipelineResourceStatus.Completed, state.Status));
    }

    [Fact]
    public async Task TransformMany_MixedExpansionSizes()
    {
        // Arrange
        var input = new[]
        {
            new ParentData("parent-1", new[] { "a" }),                  // 1 child
            new ParentData("parent-2", Array.Empty<string>()),         // 0 children
            new ParentData("parent-3", new[] { "b", "c", "d" }),      // 3 children
            new ParentData("parent-4", new[] { "e", "f" })            // 2 children
        };

        var context = new PipelineContext(
            Guid.NewGuid(),
            typeof(ParentData),
            CancellationToken.None,
            NullLogger.Instance);

        var results = new ConcurrentBag<ChildData>();

        // Act
        await DataFlowPipeline<ParentData>
            .Create(context, PipelineSource<ParentData>.FromEnumerable(input), d => d.Id)
            .TransformMany(
                parent => Task.FromResult<IEnumerable<ChildData>>(
                    parent.Children.Select(child =>
                        new ChildData(parent.Id, child, 1)).ToList()),
                child => child.ChildId,
                "ExpandChildren")
            .Action(child =>
            {
                results.Add(child);
                return Task.CompletedTask;
            }, "Collect")
            .Complete();

        // Assert
        Assert.Equal(6, results.Count); // 1 + 0 + 3 + 2
    }

    [Fact]
    public async Task TransformMany_WithErrorInChild_ContinuesWithOthers()
    {
        // Arrange
        var input = new[]
        {
            new ParentData("parent-1", new[] { "good-1", "bad-1", "good-2" })
        };

        var context = new PipelineContext(
            Guid.NewGuid(),
            typeof(ParentData),
            CancellationToken.None,
            NullLogger.Instance);

        var results = new ConcurrentBag<ProcessedChild>();

        // Act
        await DataFlowPipeline<ParentData>
            .Create(context, PipelineSource<ParentData>.FromEnumerable(input), d => d.Id)
            .TransformMany(
                parent => Task.FromResult<IEnumerable<ChildData>>(
                    parent.Children.Select(child =>
                        new ChildData(parent.Id, child, 1)).ToList()),
                child => child.ChildId,
                "ExpandChildren")
            .Transform(child =>
            {
                if (child.ChildId.Contains("bad"))
                    return Task.FromException<ProcessedChild>(new InvalidOperationException("Bad child"));

                return Task.FromResult(new ProcessedChild(child.ParentId, child.ChildId, child.Value * 10));
            }, "ProcessChild")
            .Action(processed =>
            {
                results.Add(processed);
                return Task.CompletedTask;
            }, "Collect")
            .Complete();

        // Assert
        Assert.Equal(2, results.Count); // Only good children
        Assert.All(results, r => Assert.Contains("good", r.ChildId));
    }
}

