using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using ThirdOpinion.Common.DataFlow.Core;
using ThirdOpinion.Common.DataFlow.Models;
using ThirdOpinion.Common.DataFlow.Progress;

namespace ThirdOpinion.DataFlow.TestHarness.Pipelines;

/// <summary>
/// Demonstrates a large pipeline run that performs a fresh execution followed by a retry using the new PipelineSource APIs.
/// </summary>
public sealed class LargeRetryPipelineRunner
{
    private readonly IPipelineContextFactory _contextFactory;
    private readonly IPipelineProgressService _progressService;
    private readonly ILogger<LargeRetryPipelineRunner> _logger;

    public LargeRetryPipelineRunner(
        IPipelineContextFactory contextFactory,
        IPipelineProgressService progressService,
        ILogger<LargeRetryPipelineRunner> logger)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _progressService = progressService ?? throw new ArgumentNullException(nameof(progressService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var dataSet = CreateSyntheticDataset(150).ToList();
        var failureIds = dataSet
            .Where((_, index) => (index + 1) % 6 == 0) // Fail roughly every 6th record
            .Select(record => record.Id)
            .ToHashSet(StringComparer.Ordinal);

        var resourceMap = dataSet.ToDictionary(record => record.Id, StringComparer.Ordinal);

        var freshContext = BuildContext<RawRecord>(
            name: "LargeRetryPipeline-Fresh",
            runType: PipelineRunType.Fresh,
            parentRunId: null,
            cancellationToken);

        var freshResults = new ConcurrentBag<ProcessedRecord>();

        await RunPipelineAsync(
            freshContext,
            PipelineSource<RawRecord>.FromEnumerable(dataSet),
            failureIds,
            simulateFailures: true,
            freshResults,
            cancellationToken).ConfigureAwait(false);

        var incompleteAfterFresh = await _progressService
            .GetIncompleteResourceIdsAsync(freshContext.RunId, cancellationToken)
            .ConfigureAwait(false);

        await _progressService
            .CompleteRunAsync(freshContext.RunId,
                incompleteAfterFresh.Count > 0 ? PipelineRunStatus.Failed : PipelineRunStatus.Completed,
                cancellationToken)
            .ConfigureAwait(false);

        LogRunSummary(
            freshContext,
            freshResults.Count,
            failureIds.Count,
            incompleteAfterFresh.Count,
            "Fresh run completed");

        if (incompleteAfterFresh.Count == 0)
        {
            _logger.LogInformation("No incomplete resources detected; skipping retry run.");
            return;
        }

        var retryContext = BuildContext<RawRecord>(
            name: "LargeRetryPipeline-Retry",
            runType: PipelineRunType.Retry,
            parentRunId: freshContext.RunId,
            cancellationToken);

        var retrySource = PipelineSource<RawRecord>.FromRunType(
            _progressService,
            freshSourceFactory: () => dataSet,
            loadIncompleteAsync: (ids, ct) => LoadRetryRecordsAsync(ids, resourceMap, ct));

        var retryResults = new ConcurrentBag<ProcessedRecord>();

        await RunPipelineAsync(
            retryContext,
            retrySource,
            failureIds,
            simulateFailures: false,
            retryResults,
            cancellationToken).ConfigureAwait(false);

        var incompleteAfterRetry = await _progressService
            .GetIncompleteResourceIdsAsync(retryContext.RunId, cancellationToken)
            .ConfigureAwait(false);

        await _progressService
            .CompleteRunAsync(retryContext.RunId,
                incompleteAfterRetry.Count > 0 ? PipelineRunStatus.Failed : PipelineRunStatus.Completed,
                cancellationToken)
            .ConfigureAwait(false);

        LogRunSummary(
            retryContext,
            retryResults.Count,
            0,
            incompleteAfterRetry.Count,
            "Retry run completed");
    }

    private async Task RunPipelineAsync(
        IPipelineContext context,
        PipelineSource<RawRecord> source,
        HashSet<string> failureIds,
        bool simulateFailures,
        ConcurrentBag<ProcessedRecord> sink,
        CancellationToken cancellationToken)
    {
        await DataFlowPipeline<RawRecord>
            .Create(context, record => record.Id)
            .WithSource(source)
            .Transform(NormalizeAsync, "Normalize")
            .Transform(record => ScoreAsync(record, failureIds, simulateFailures), "Score")
            .Action(result =>
            {
                sink.Add(result);
                return Task.CompletedTask;
            }, "Persist")
            .Complete(result => result.Id)
            .ConfigureAwait(false);
    }

    private IPipelineContext BuildContext<TResource>(
        string name,
        PipelineRunType runType,
        Guid? parentRunId,
        CancellationToken cancellationToken)
    {
        return _contextFactory
            .CreateBuilder<TResource>()
            .WithRunId(Guid.NewGuid())
            .WithCategory("Harness")
            .WithName(name)
            .WithRunType(runType)
            .WithParentRunId(parentRunId)
            .WithCancellationToken(cancellationToken)
            .WithDefaultMaxDegreeOfParallelism(6)
            .Build();
    }

    private void LogRunSummary(
        IPipelineContext context,
        int successes,
        int simulatedFailures,
        int remainingIncomplete,
        string message)
    {
        _logger.LogInformation(
            "{Message}: RunId={RunId}, ParentRunId={ParentRunId}, Successes={Successes}, SimulatedFailures={SimulatedFailures}, RemainingIncomplete={RemainingIncomplete}",
            message,
            context.RunId,
            context.ParentRunId,
            successes,
            simulatedFailures,
            remainingIncomplete);
    }

    private static IEnumerable<RawRecord> CreateSyntheticDataset(int count)
    {
        var random = new Random(42);
        for (var i = 1; i <= count; i++)
        {
            var id = $"RR-{i:000}";
            var age = random.Next(25, 85);
            var hasTargetedBiomarker = random.NextDouble() >= 0.5;
            var biomarkers = CreateBiomarkers(random);

            yield return new RawRecord(id, age, hasTargetedBiomarker, biomarkers);
        }
    }

    private static IReadOnlyList<string> CreateBiomarkers(Random random)
    {
        var known = new[]
        {
            "ALK", "BRCA1", "BRCA2", "EGFR", "HER2", "KRAS", "NRAS", "PD-L1", "ROS1", "TP53", "BRAF"
        };

        return Enumerable.Range(0, random.Next(1, 4))
            .Select(_ => known[random.Next(known.Length)])
            .Distinct()
            .ToArray();
    }

    private static async Task<NormalizedRecord> NormalizeAsync(RawRecord record)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(5)).ConfigureAwait(false);

        var normalizedBiomarkers = record.Biomarkers
            .Select(b => b.Trim().ToUpperInvariant())
            .OrderBy(b => b)
            .ToArray();

        var ageBand = record.Age switch
        {
            >= 70 => "Elderly",
            >= 55 => "Senior",
            >= 40 => "MiddleAge",
            _ => "Adult"
        };

        return new NormalizedRecord(record.Id, record.Age, record.HasTargetedBiomarker, normalizedBiomarkers, ageBand);
    }

    private static Task<ProcessedRecord> ScoreAsync(
        NormalizedRecord record,
        HashSet<string> failureIds,
        bool simulateFailures)
    {
        if (simulateFailures && failureIds.Contains(record.Id))
        {
            return Task.FromException<ProcessedRecord>(
                new InvalidOperationException($"Simulated processing failure for resource {record.Id}."));
        }

        var baseScore = 35;
        var biomarkerBonus = record.HasTargetedBiomarker ? 40 : 12;
        var ageBonus = record.AgeBand switch
        {
            "Elderly" => 10,
            "Senior" => 8,
            "MiddleAge" => 6,
            _ => 4
        };

        var densityBonus = Math.Min(record.Biomarkers.Count * 4, 16);
        var totalScore = baseScore + biomarkerBonus + ageBonus + densityBonus;

        return Task.FromResult(new ProcessedRecord(
            record.Id,
            record.Age,
            record.Biomarkers,
            totalScore,
            totalScore >= 80));
    }

    private static async IAsyncEnumerable<RawRecord> LoadRetryRecordsAsync(
        IEnumerable<string> resourceIds,
        IReadOnlyDictionary<string, RawRecord> map,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var resourceId in resourceIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (map.TryGetValue(resourceId, out var record))
            {
                yield return record;
            }

            await Task.Yield();
        }
    }

    private sealed record RawRecord(
        string Id,
        int Age,
        bool HasTargetedBiomarker,
        IReadOnlyList<string> Biomarkers);

    private sealed record NormalizedRecord(
        string Id,
        int Age,
        bool HasTargetedBiomarker,
        IReadOnlyList<string> Biomarkers,
        string AgeBand);

    private sealed record ProcessedRecord(
        string Id,
        int Age,
        IReadOnlyList<string> Biomarkers,
        int Score,
        bool IsEligible);
}

