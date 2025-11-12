using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using ThirdOpinion.Common.DataFlow.Core;
using ThirdOpinion.Common.DataFlow.Models;
using ThirdOpinion.Common.DataFlow.Progress;

namespace ThirdOpinion.DataFlow.TestHarness.Pipelines;

public sealed class SamplePipelineRunner
{
    private readonly IPipelineContextFactory _contextFactory;
    private readonly IPipelineProgressService _progressService;
    private readonly ILogger<SamplePipelineRunner> _logger;

    public SamplePipelineRunner(
        IPipelineContextFactory contextFactory,
        IPipelineProgressService progressService,
        ILogger<SamplePipelineRunner> logger)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _progressService = progressService ?? throw new ArgumentNullException(nameof(progressService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var context = _contextFactory
            .CreateBuilder<RawPatientRecord>()
            .WithRunId(Guid.NewGuid())
            .WithCategory("Harness")
            .WithName("SamplePipeline")
            .WithCancellationToken(cancellationToken)
            .WithDefaultMaxDegreeOfParallelism(4)
            .Build();

        var runId = context.RunId;
        var runCategory = context.Category;
        var runName = context.Name;

        try
        {
            var processed = new ConcurrentBag<ScoredPatientRecord>();

            await DataFlowPipeline<RawPatientRecord>
                .Create(context, record => record.Id)
                .FromEnumerable(CreateSampleData())
                .Transform(NormalizeAsync, "NormalizeInputs")
                .Transform(ScorePatient, "Score")
                .Action(result => PersistResultAsync(processed, result), "Persist")
                .Complete(result => result.Id)
                .ConfigureAwait(false);

            await _progressService.CompleteRunAsync(runId, PipelineRunStatus.Completed, cancellationToken).ConfigureAwait(false);

            var resultsSnapshot = processed.ToArray();

            _logger.LogInformation("Pipeline run {RunId} ({Category}/{Name}) processed {Count} patients with {EligibleCount} matches.",
                runId,
                runCategory,
                runName,
                resultsSnapshot.Length,
                resultsSnapshot.Count(r => r.IsEligible));

            foreach (var record in resultsSnapshot)
            {
                _logger.LogInformation(
                    "Patient {Id} | Age {Age} ({AgeBand}) | Biomarkers: {BiomarkerSummary} | Targeted: {TargetedBiomarker} | Score: {Score} | Eligible: {Eligible}",
                    record.Id,
                    record.Age,
                    record.AgeBand,
                    string.Join(", ", record.Biomarkers),
                    record.HasTargetedBiomarker,
                    record.MatchScore,
                    record.IsEligible);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pipeline run {RunId} ({Category}/{Name}) failed. Marking as failed in persistence.", runId, runCategory, runName);
            await _progressService.CompleteRunAsync(runId, PipelineRunStatus.Failed, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static IEnumerable<RawPatientRecord> CreateSampleData()
    {
        yield return new RawPatientRecord("PT-001", 63, true, ["PD-L1", "BRCA1"]);
        yield return new RawPatientRecord("PT-002", 57, false, ["EGFR"]);
        yield return new RawPatientRecord("PT-003", 71, true, ["ALK", "ROS1"]);
        yield return new RawPatientRecord("PT-004", 49, false, ["TP53"]);
        yield return new RawPatientRecord("PT-005", 66, true, ["PD-L1", "HER2"]);
    }

    private static async Task<NormalizedPatientRecord> NormalizeAsync(RawPatientRecord record)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(25)).ConfigureAwait(false);

        var normalizedBiomarkers = record.Biomarkers
            .Select(biomarker => biomarker.Trim().ToUpperInvariant())
            .OrderBy(biomarker => biomarker)
            .ToArray();

        return new NormalizedPatientRecord(
            record.Id,
            record.Age,
            record.HasTargetedBiomarker,
            normalizedBiomarkers,
            record.Age >= 65 ? "Senior" : record.Age >= 50 ? "MiddleAge" : "Adult");
    }

    private static ScoredPatientRecord ScorePatient(NormalizedPatientRecord record)
    {
        const int baseScore = 40;
        var biomarkerBonus = record.HasTargetedBiomarker ? 35 : 10;
        var ageBonus = record.AgeBand switch
        {
            "Senior" => 15,
            "MiddleAge" => 10,
            _ => 5
        };

        var biomarkerDensityBonus = Math.Min(record.Biomarkers.Count * 5, 20);
        var totalScore = baseScore + biomarkerBonus + ageBonus + biomarkerDensityBonus;

        return new ScoredPatientRecord(
            record.Id,
            record.Age,
            record.HasTargetedBiomarker,
            record.Biomarkers,
            record.AgeBand,
            totalScore,
            totalScore >= 75);
    }

    private static Task PersistResultAsync(ConcurrentBag<ScoredPatientRecord> sink, ScoredPatientRecord record)
    {
        sink.Add(record);
        return Task.CompletedTask;
    }

    private sealed record RawPatientRecord(string Id, int Age, bool HasTargetedBiomarker, IReadOnlyList<string> Biomarkers);

    private sealed record NormalizedPatientRecord(
        string Id,
        int Age,
        bool HasTargetedBiomarker,
        IReadOnlyList<string> Biomarkers,
        string AgeBand);

    private sealed record ScoredPatientRecord(
        string Id,
        int Age,
        bool HasTargetedBiomarker,
        IReadOnlyList<string> Biomarkers,
        string AgeBand,
        int MatchScore,
        bool IsEligible);
}
