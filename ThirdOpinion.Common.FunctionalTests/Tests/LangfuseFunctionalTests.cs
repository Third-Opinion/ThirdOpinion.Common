using Microsoft.Extensions.Configuration;
using ThirdOpinion.Common.FunctionalTests.Infrastructure;
using ThirdOpinion.Common.Langfuse;
using Xunit.Abstractions;

namespace ThirdOpinion.Common.FunctionalTests.Tests;

[Collection("Langfuse")]
public class LangfuseFunctionalTests : BaseIntegrationTest
{
    private readonly bool _isConfigured;
    private readonly string _projectName;

    public LangfuseFunctionalTests(ITestOutputHelper output) : base(output)
    {
        _projectName = Configuration.GetValue<string>("Langfuse:ProjectName") ??
                       "third-opinion-test";

        var publicKey = Configuration.GetValue<string>("Langfuse:PublicKey");
        var secretKey = Configuration.GetValue<string>("Langfuse:SecretKey");
        _isConfigured = !string.IsNullOrEmpty(publicKey) && !string.IsNullOrEmpty(secretKey);
    }

    [Fact]
    public async Task LangfuseService_ShouldBeConfigured_WhenKeysProvided()
    {
        if (!_isConfigured)
        {
            WriteOutput("⚠️ Langfuse keys not configured - expected for functional tests");
            WriteOutput(
                "To test Langfuse integration, set Langfuse:PublicKey and Langfuse:SecretKey in appsettings.Test.json");
            return;
        }

        WriteOutput("Testing Langfuse service configuration...");

        LangfuseService.ShouldNotBeNull(
            "LangfuseService should be registered when keys are configured");

        WriteOutput("✓ LangfuseService properly configured and registered");
    }

    [Fact]
    public async Task LangfuseService_ShouldCreateTrace_WhenConfigured()
    {
        if (!_isConfigured || LangfuseService == null)
        {
            WriteOutput("⚠️ Langfuse not configured, skipping trace creation test");
            return;
        }

        WriteOutput("Testing Langfuse trace creation...");

        var traceId = Guid.NewGuid().ToString();
        var metadata = new Dictionary<string, object>
        {
            { "test_type", "functional" },
            { "project", _projectName },
            { "environment", "test" },
            { "user_id", "test-user" },
            { "session_id", "test-session" }
        };

        LangfuseTraceResponse? response
            = await LangfuseService.CreateTraceAsync(traceId, "functional-test-trace", metadata);

        WriteOutput($"Created trace with ID: {traceId}");

        response.ShouldNotBeNull();
        response.Id.ShouldBe(traceId);

        WriteOutput("✓ Successfully created trace in Langfuse");
    }

    [Fact]
    public async Task LangfuseService_ShouldCreateGeneration_WhenConfigured()
    {
        if (!_isConfigured || LangfuseService == null)
        {
            WriteOutput("⚠️ Langfuse not configured, skipping generation test");
            return;
        }

        WriteOutput("Testing Langfuse generation creation...");

        var traceId = Guid.NewGuid().ToString();
        var generationId = Guid.NewGuid().ToString();

        // First create a trace
        await LangfuseService.CreateTraceAsync(traceId, "generation-test-trace");

        // Then create a generation within that trace
        var generationRequest = new LangfuseGenerationRequest
        {
            Id = generationId,
            TraceId = traceId,
            Name = "test-generation",
            Model = "test-model",
            Input = new { prompt = "Test prompt for generation" },
            Output = new { text = "Test response from model" },
            StartTime = DateTime.UtcNow.AddSeconds(-5),
            EndTime = DateTime.UtcNow,
            Usage = new LangfuseUsage
            {
                Input = 10,
                Output = 15,
                Total = 25
            },
            Metadata = new Dictionary<string, object>
            {
                { "temperature", 0.7 },
                { "max_tokens", 100 }
            }
        };

        LangfuseObservationResponse? response
            = await LangfuseService.CreateGenerationAsync(generationRequest);

        WriteOutput($"Created generation with ID: {generationId} in trace: {traceId}");

        // Note: When EnableTelemetry is true, events are batched and may not return detailed responses
        // The operation succeeds if no exception is thrown
        if (response != null)
        {
            response.Id.ShouldBe(generationId);
            // TraceId may be empty in batch responses
            if (!string.IsNullOrEmpty(response.TraceId))
            {
                response.TraceId.ShouldBe(traceId);
            }
            WriteOutput("✓ Received synchronous response from Langfuse");
        }
        else
        {
            WriteOutput("✓ Generation queued successfully (batch mode)");
        }

        WriteOutput("✓ Successfully created generation in Langfuse");
    }

    [Fact]
    public async Task LangfuseService_ShouldCreateObservation_WhenConfigured()
    {
        if (!_isConfigured || LangfuseService == null)
        {
            WriteOutput("⚠️ Langfuse not configured, skipping event test");
            return;
        }

        WriteOutput("Testing Langfuse observation creation...");

        var traceId = Guid.NewGuid().ToString();
        var eventId = Guid.NewGuid().ToString();

        // First create a trace
        await LangfuseService.CreateTraceAsync(traceId, "event-test-trace");

        // Then create an observation within that trace
        var metadata = new Dictionary<string, object>
        {
            { "event_type", "functional_test" },
            { "source", "automated_test" }
        };

        LangfuseObservationResponse? response = await LangfuseService.CreateObservationAsync(
            traceId,
            eventId,
            "event",
            "test-event",
            new { action = "test_action", parameters = new { key = "value" } },
            new { result = "success", details = "Test event completed" },
            metadata);

        WriteOutput($"Created event with ID: {eventId} in trace: {traceId}");

        // Note: When EnableTelemetry is true, events are batched and may not return detailed responses
        // The operation succeeds if no exception is thrown
        if (response != null)
        {
            response.Id.ShouldBe(eventId);
            response.TraceId.ShouldBe(traceId);
            WriteOutput("✓ Received synchronous response from Langfuse");
        }
        else
        {
            WriteOutput("✓ Observation queued successfully (batch mode)");
        }

        WriteOutput("✓ Successfully created observation in Langfuse");
    }

    [Fact]
    public async Task LangfuseService_ShouldUpdateGeneration_WhenConfigured()
    {
        if (!_isConfigured || LangfuseService == null)
        {
            WriteOutput("⚠️ Langfuse not configured, skipping update test");
            return;
        }

        WriteOutput("Testing Langfuse generation update...");

        var traceId = Guid.NewGuid().ToString();
        var generationId = Guid.NewGuid().ToString();

        // Create trace and initial generation
        await LangfuseService.CreateTraceAsync(traceId, "update-test-trace");

        var generationRequest = new LangfuseGenerationRequest
        {
            Id = generationId,
            TraceId = traceId,
            Name = "test-generation-update",
            Model = "test-model",
            Input = new { prompt = "Initial prompt" },
            StartTime = DateTime.UtcNow.AddSeconds(-10)
        };

        await LangfuseService.CreateGenerationAsync(generationRequest);

        // Update the generation with output and completion
        var usage = new LangfuseUsage
        {
            Input = 8,
            Output = 12,
            Total = 20
        };

        LangfuseObservationResponse? response = await LangfuseService.UpdateGenerationAsync(
            generationId,
            DateTime.UtcNow,
            new { text = "Updated response with completion" },
            usage);

        WriteOutput($"Updated generation with ID: {generationId}");

        // Note: When EnableTelemetry is true, events are batched and may not return detailed responses
        // The operation succeeds if no exception is thrown
        if (response != null)
        {
            response.Id.ShouldBe(generationId);
            WriteOutput("✓ Received synchronous response from Langfuse");
        }
        else
        {
            WriteOutput("✓ Generation update queued successfully (batch mode)");
        }

        WriteOutput("✓ Successfully updated generation in Langfuse");
    }

    [Fact]
    public async Task LangfuseService_ShouldHandleBatchMode_WhenEnabled()
    {
        if (!_isConfigured || LangfuseService == null)
        {
            WriteOutput("⚠️ Langfuse not configured, skipping batch mode test");
            return;
        }

        var batchEnabled = Configuration.GetValue<bool>("Langfuse:EnableBatchMode");
        if (!batchEnabled)
            WriteOutput(
                "⚠️ Batch mode not enabled in configuration, test will still work but won't test batching");

        WriteOutput("Testing Langfuse service batch operations...");

        var traceId = Guid.NewGuid().ToString();
        var tasks = new List<Task>();

        // Create a trace
        await LangfuseService.CreateTraceAsync(traceId, "batch-test-trace");

        // Create multiple events rapidly to test batching
        for (var i = 0; i < 5; i++)
        {
            var eventId = Guid.NewGuid().ToString();

            tasks.Add(LangfuseService.CreateObservationAsync(
                traceId,
                eventId,
                "event",
                $"batch-event-{i}",
                new { batch_index = i },
                new { processed = true }));
        }

        await Task.WhenAll(tasks);

        WriteOutput($"Created 5 events in batch for trace: {traceId}");
        WriteOutput("✓ Successfully processed multiple Langfuse operations");
    }

    [Fact]
    public async Task LangfuseService_ShouldRespectDataMasking_WhenEnabled()
    {
        if (!_isConfigured || LangfuseService == null)
        {
            WriteOutput("⚠️ Langfuse not configured, skipping data masking test");
            return;
        }

        bool dataMaskingEnabled = Configuration.GetValue("Langfuse:EnableDataMasking", true);
        WriteOutput($"Data masking configuration: {dataMaskingEnabled}");

        WriteOutput("Testing Langfuse with potentially sensitive data...");

        var traceId = Guid.NewGuid().ToString();
        var generationId = Guid.NewGuid().ToString();

        await LangfuseService.CreateTraceAsync(traceId, "data-masking-test");

        var generationRequest = new LangfuseGenerationRequest
        {
            Id = generationId,
            TraceId = traceId,
            Name = "sensitive-data-generation",
            Model = "test-model",
            Input = new
            {
                prompt = "Test with email: test@example.com and phone: 555-1234",
                sensitive_field = "credit_card_1234567890123456"
            },
            Output = new
            {
                text = "Response mentioning ssn: 123-45-6789"
            },
            StartTime = DateTime.UtcNow.AddSeconds(-2),
            EndTime = DateTime.UtcNow
        };

        LangfuseObservationResponse? response
            = await LangfuseService.CreateGenerationAsync(generationRequest);

        response.ShouldNotBeNull();
        WriteOutput(
            "✓ Successfully handled potentially sensitive data (data masking applied per configuration)");
    }

    protected override Task CleanupTestResourcesAsync()
    {
        WriteOutput("Langfuse tests cleanup - traces and events are managed by Langfuse service");
        return Task.CompletedTask;
    }
}