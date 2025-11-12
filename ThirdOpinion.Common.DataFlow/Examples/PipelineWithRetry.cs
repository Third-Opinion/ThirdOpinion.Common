using Microsoft.Extensions.Logging.Abstractions;
using Polly;
using Polly.Retry;
using ThirdOpinion.Common.DataFlow.Core;

namespace ThirdOpinion.Common.DataFlow.Examples;

/// <summary>
/// Example of a pipeline with Polly retry policies for resilience
/// </summary>
public class PipelineWithRetry
{
    public record ApiRequest(string Id, string Endpoint);
    public record ApiResponse(string Id, string Data, int AttemptCount);

    private static int _failureCount = 0;

    public static async Task RunExample()
    {
        var requests = new List<ApiRequest>
        {
            new("req1", "/api/data/1"),
            new("req2", "/api/data/2"),
            new("req3", "/api/data/3")
        };

        // Create Polly retry policy
        var retryPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(100),
                BackoffType = DelayBackoffType.Exponential,
                OnRetry = args =>
                {
                    Console.WriteLine($"  Retry attempt {args.AttemptNumber} after {args.RetryDelay}");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();

        var context = new PipelineContext(
            Guid.NewGuid(),
            typeof(ApiRequest),
            CancellationToken.None,
            NullLogger.Instance);

        var results = new List<ApiResponse>();

        await DataFlowPipeline<ApiRequest>
            .Create(context, req => req.Id)
            .FromEnumerable(requests)
            .Transform(req => CallApiWithRetry(req, retryPipeline), "CallApi")
            .Action(response =>
            {
                results.Add(response);
                return Task.CompletedTask;
            }, "Collect")
            .Complete();

        Console.WriteLine("\nPipeline completed with retry logic!");
        Console.WriteLine($"Processed {results.Count} requests:");
        foreach (var result in results)
        {
            Console.WriteLine($"  {result.Id}: {result.Data} (Attempts: {result.AttemptCount})");
        }
    }

    private static async Task<ApiResponse> CallApiWithRetry(
        ApiRequest request,
        ResiliencePipeline retryPipeline)
    {
        var attemptCount = 0;

        var result = await retryPipeline.ExecuteAsync(async ct =>
        {
            attemptCount++;
            return await SimulateApiCall(request, attemptCount);
        });

        return new ApiResponse(request.Id, result, attemptCount);
    }

    private static async Task<string> SimulateApiCall(ApiRequest request, int attempt)
    {
        await Task.Delay(50);

        // Simulate transient failures
        if (attempt < 2 && _failureCount++ % 3 == 0)
        {
            throw new Exception($"Transient API error for {request.Id}");
        }

        return $"Data from {request.Endpoint}";
    }
}

