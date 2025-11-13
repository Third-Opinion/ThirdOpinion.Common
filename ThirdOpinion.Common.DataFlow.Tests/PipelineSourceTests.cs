using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using ThirdOpinion.Common.DataFlow.Core;
using ThirdOpinion.Common.DataFlow.Models;
using ThirdOpinion.Common.DataFlow.Progress;
using ThirdOpinion.Common.DataFlow.Progress.Models;

namespace ThirdOpinion.Common.DataFlow.Tests;

public class PipelineSourceTests
{
    [Fact]
    public async Task FromEnumerable_ProducesAllItems()
    {
        var context = CreateContext<Poco>(PipelineRunType.Fresh);
        var results = new ConcurrentBag<string>();

        await DataFlowPipeline<Poco>
            .Create(context, PipelineSource<Poco>.FromEnumerable(CreatePocos(3)), p => p.Id)
            .Transform(p => Task.FromResult(p.Id.ToUpperInvariant()), "Upper")
            .Action(id =>
            {
                results.Add(id);
                return Task.CompletedTask;
            }, "Collect")
            .Complete();

        Assert.Equal(new[] { "A", "B", "C" }, results.OrderBy(r => r));
    }

    [Fact]
    public async Task FromAsyncEnumerable_StreamsValues()
    {
        var context = CreateContext<int>(PipelineRunType.Fresh);
        var results = new List<int>();

        var source = PipelineSource<int>.FromAsyncEnumerable((_, ct) => GenerateAsync(ct, 1, 5));

        await DataFlowPipeline<int>
            .Create(context, source, i => i.ToString())
            .Transform(i => Task.FromResult(i * 2), "Double")
            .Action(result =>
            {
                results.Add(result);
                return Task.CompletedTask;
            }, "Collect")
            .Complete();

        Assert.Equal(new[] { 2, 4, 6, 8, 10 }, results.OrderBy(i => i));
    }

    [Fact]
    public async Task WithSource_ReplacesPreviousDefinition()
    {
        var context = CreateContext<int>(PipelineRunType.Fresh);
        var results = new List<int>();

        await DataFlowPipeline<int>
            .Create(context, i => i.ToString())
            .WithSource(PipelineSource<int>.FromEnumerable(new[] { 1 }))
            .WithSource(PipelineSource<int>.FromEnumerable(new[] { 42 }))
            .Transform(i => Task.FromResult(i), "Identity")
            .Action(result =>
            {
                results.Add(result);
                return Task.CompletedTask;
            }, "Collect")
            .Complete();

        Assert.Equal(new[] { 42 }, results);
    }

    [Fact]
    public async Task MissingSource_ThrowsInvalidOperation()
    {
        var context = CreateContext<int>(PipelineRunType.Fresh);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DataFlowPipeline<int>
                .Create(context, i => i.ToString())
                .Transform(i => Task.FromResult(i), "Identity")
                .Complete());
    }

    [Fact]
    public async Task FromRunType_Fresh_UsesFreshSourceFactory()
    {
        var fresh = new[] { new Poco("fresh-1"), new Poco("fresh-2") };
        var progressService = new StubProgressService(Array.Empty<string>());
        var context = CreateContext<Poco>(PipelineRunType.Fresh);

        var results = new List<string>();

        var source = PipelineSource<Poco>.FromRunType(
            progressService,
            freshSourceFactory: () => fresh,
            loadIncompleteAsync: (ids, ct) => FetchByIds(ids, fresh.ToDictionary(p => p.Id), ct));

        await DataFlowPipeline<Poco>
            .Create(context, source, p => p.Id)
            .Transform(p => Task.FromResult(p.Id), "PassThrough")
            .Action(id =>
            {
                results.Add(id);
                return Task.CompletedTask;
            }, "Collect")
            .Complete();

        Assert.Equal(new[] { "fresh-1", "fresh-2" }, results.OrderBy(id => id));
        Assert.Equal(0, progressService.GetIncompleteCalls);
        Assert.Null(progressService.LastParentRunId);
    }

    [Fact]
    public async Task FromRunType_Retry_UsesIncompleteResources()
    {
        var fresh = new[] { new Poco("item-1"), new Poco("item-2"), new Poco("item-3") };
        var retryMap = fresh.ToDictionary(p => p.Id);
        var progressService = new StubProgressService(new[] { "item-2", "item-3" });
        var parentRunId = Guid.NewGuid();
        var context = CreateContext<Poco>(PipelineRunType.Retry, parentRunId);
        var results = new List<string>();

        var source = PipelineSource<Poco>.FromRunType(
            progressService,
            freshSourceFactory: () => fresh,
            loadIncompleteAsync: (ids, ct) => FetchByIds(ids, retryMap, ct));

        await DataFlowPipeline<Poco>
            .Create(context, source, p => p.Id)
            .Transform(p => Task.FromResult(p.Id), "PassThrough")
            .Action(id =>
            {
                results.Add(id);
                return Task.CompletedTask;
            }, "Collect")
            .Complete();

        Assert.Equal(new[] { "item-2", "item-3" }, results.OrderBy(id => id));
        Assert.Equal(1, progressService.GetIncompleteCalls);
        Assert.Equal(parentRunId, progressService.LastParentRunId);
    }

    private static PipelineContext CreateContext<T>(PipelineRunType runType, Guid? parentRunId = null)
    {
        return new PipelineContext(
            runId: Guid.NewGuid(),
            resourceType: typeof(T),
            cancellationToken: CancellationToken.None,
            logger: NullLogger.Instance,
            progressTracker: null,
            artifactBatcher: null,
            resourceRunCache: null,
            defaultStepOptions: null,
            category: "Tests",
            name: "PipelineSource",
            runType: runType,
            parentRunId: parentRunId);
    }

    private static IEnumerable<Poco> CreatePocos(int count)
    {
        for (var i = 0; i < count; i++)
        {
            yield return new Poco(((char)('a' + i)).ToString());
        }
    }

    private static async IAsyncEnumerable<int> GenerateAsync([EnumeratorCancellation] CancellationToken ct, int start, int count)
    {
        for (int i = start; i < start + count; i++)
        {
            ct.ThrowIfCancellationRequested();
            yield return i;
            await Task.Yield();
        }
    }

    private static async IAsyncEnumerable<Poco> FetchByIds(
        IEnumerable<string> ids,
        IReadOnlyDictionary<string, Poco> source,
        [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            if (source.TryGetValue(id, out var value))
            {
                yield return value;
            }

            await Task.Yield();
        }
    }

    private sealed record Poco(string Id);

    private sealed class StubProgressService : IPipelineProgressService
    {
        private readonly List<string> _incompleteIds;

        public StubProgressService(IEnumerable<string> incompleteIds)
        {
            _incompleteIds = incompleteIds.ToList();
        }

        public int GetIncompleteCalls { get; private set; }

        public Guid? LastParentRunId { get; private set; }

        public Task<PipelineRun> CreateRunAsync(CreatePipelineRunRequest request, CancellationToken ct) =>
            Task.FromResult(new PipelineRun
            {
                Id = request.RunId ?? Guid.NewGuid(),
                Category = request.Category,
                Name = request.Name,
                RunType = request.RunType,
                Status = PipelineRunStatus.Running,
                ParentRunId = request.ParentRunId,
                StartTime = DateTime.UtcNow
            });

        public Task CompleteRunAsync(Guid runId, PipelineRunStatus finalStatus, CancellationToken ct) =>
            Task.CompletedTask;

        public Task<List<string>> GetIncompleteResourceIdsAsync(Guid parentRunId, CancellationToken ct)
        {
            GetIncompleteCalls++;
            LastParentRunId = parentRunId;
            return Task.FromResult(_incompleteIds);
        }

        public Task<Guid?> GetResourceRunIdAsync(Guid runId, string resourceId, CancellationToken ct) =>
            Task.FromResult<Guid?>(null);

        public Task CreateResourceRunsBatchAsync(Guid runId, ResourceProgressUpdate[] updates, CancellationToken ct) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<StepProgressUpdate>> UpdateStepProgressBatchAsync(Guid runId, StepProgressUpdate[] updates, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<StepProgressUpdate>>(Array.Empty<StepProgressUpdate>());

        public Task CompleteResourceRunsBatchAsync(Guid runId, ResourceCompletionUpdate[] updates, CancellationToken ct) =>
            Task.CompletedTask;
    }
}

