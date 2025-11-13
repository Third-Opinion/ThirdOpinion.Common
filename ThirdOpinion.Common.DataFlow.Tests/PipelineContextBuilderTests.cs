using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.DependencyInjection;
using ThirdOpinion.Common.DataFlow.Core;
using ThirdOpinion.Common.DataFlow.Services.InMemory;
using static ThirdOpinion.Common.DataFlow.Tests.TestTimings;

namespace ThirdOpinion.Common.DataFlow.Tests;

public class PipelineContextBuilderTests
{
    [Fact]
    public void Build_UsesDefaultPipelineStepOptionsWhenNotOverridden()
    {
        // Arrange
        var context = InMemoryServiceFactory.CreateMinimalContext(
            typeof(string),
            "Test",
            "TestPipeline",
            CancellationToken.None);

        // Act
        var defaults = context.DefaultStepOptions;

        // Assert
        Assert.Equal(DataflowBlockOptions.Unbounded, defaults.MaxDegreeOfParallelism);
        Assert.Equal(DataflowBlockOptions.Unbounded, defaults.BoundedCapacity);
        Assert.True(defaults.EnableProgressTracking);
    }

    [Fact]
    public void WithDefaultStepOptions_ClonesInstance()
    {
        // Arrange
        var originalOptions = new PipelineStepOptions
        {
            MaxDegreeOfParallelism = 8,
            BoundedCapacity = 512,
            EnableProgressTracking = false
        };

        var builder = new PipelineContextBuilder(
            resourceType: typeof(string),
            progressTrackerFactory: null,
            artifactBatcherFactory: null,
            resourceRunCache: null,
            logger: null);

        var context = builder
            .WithCategory("Test")
            .WithName("TestPipeline")
            .WithDefaultStepOptions(originalOptions)
            .Build();

        // Mutate after building to ensure cloning occurred
        originalOptions.MaxDegreeOfParallelism = 2;
        originalOptions.BoundedCapacity = 128;
        originalOptions.EnableProgressTracking = true;

        // Act
        var defaults = context.DefaultStepOptions;

        // Assert
        Assert.Equal(8, defaults.MaxDegreeOfParallelism);
        Assert.Equal(512, defaults.BoundedCapacity);
        Assert.False(defaults.EnableProgressTracking);
    }

    [Fact]
    public void DefaultStepOptions_ReturnsClonePerAccess()
    {
        // Arrange
        var builder = new PipelineContextBuilder(
            resourceType: typeof(string),
            progressTrackerFactory: null,
            artifactBatcherFactory: null,
            resourceRunCache: null,
            logger: null);

        var context = builder
            .WithCategory("Test")
            .WithName("TestPipeline")
            .WithDefaultMaxDegreeOfParallelism(6)
            .Build();

        // Act
        var first = context.DefaultStepOptions;
        first.MaxDegreeOfParallelism = 20;

        var second = context.DefaultStepOptions;

        // Assert
        Assert.Equal(6, second.MaxDegreeOfParallelism);
    }

    [Fact]
    public async Task Transform_UsesContextDefaultParallelismWhenOptionsNotProvided()
    {
        // Arrange
        const int desiredParallelism = 3;
        var builder = new PipelineContextBuilder(
            resourceType: typeof(int),
            progressTrackerFactory: null,
            artifactBatcherFactory: null,
            resourceRunCache: null,
            logger: null);

        var context = builder
            .WithCategory("Test")
            .WithName("TestPipeline")
            .WithDefaultMaxDegreeOfParallelism(desiredParallelism)
            .Build();

        var items = Enumerable.Range(0, 20).ToList();
        var currentConcurrency = 0;
        var maxConcurrency = 0;

        // Act
        await DataFlowPipeline<int>
            .Create(context, value => value.ToString())
            .FromEnumerable(items)
            .Transform(async value =>
            {
                var inFlight = Interlocked.Increment(ref currentConcurrency);
                UpdateMaxConcurrency(ref maxConcurrency, inFlight);

                await Task.Delay(VerySlowDelayMs);

                Interlocked.Decrement(ref currentConcurrency);
                return value;
            }, "DefaultParallelismStep")
            .Action(_ => Task.CompletedTask, "Sink")
            .Complete();

        // Assert
        Assert.Equal(desiredParallelism, maxConcurrency);
    }

    private static void UpdateMaxConcurrency(ref int maxConcurrency, int observedValue)
    {
        int current;
        do
        {
            current = maxConcurrency;
            if (observedValue <= current)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref maxConcurrency, observedValue, current) != current);
    }
}


