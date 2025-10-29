using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Microsoft.Extensions.Configuration;
using ThirdOpinion.Common.FunctionalTests.Infrastructure;
using Xunit.Abstractions;
using Shouldly;
using System.Text;
using System.Text.Json;
using ThirdOpinion.Common.Aws.Bedrock;

namespace ThirdOpinion.Common.FunctionalTests.Tests;

[Collection("Bedrock")]
public class BedrockFunctionalTests : BaseIntegrationTest
{
    private readonly string _defaultModelId;
    private readonly string _streamingModelId;
    private readonly string _testPrompt;
    private readonly int _maxTokens;

    public BedrockFunctionalTests(ITestOutputHelper output) : base(output)
    {
        _defaultModelId = Configuration.GetValue<string>("Bedrock:DefaultModelId") ?? "anthropic.claude-3-haiku-20240307-v1:0";
        _streamingModelId = Configuration.GetValue<string>("Bedrock:StreamingModelId") ?? "anthropic.claude-3-sonnet-20240229-v1:0";
        _testPrompt = Configuration.GetValue<string>("Bedrock:TestPrompt") ?? "Hello, this is a test prompt for functional testing.";
        _maxTokens = Configuration.GetValue<int>("Bedrock:MaxTokens", 100);
    }

    [Fact]
    public async Task BedrockClient_ShouldListFoundationModels()
    {
        WriteOutput("Testing Bedrock client - listing foundation models...");

        var request = new Amazon.Bedrock.Model.ListFoundationModelsRequest();
        var response = await BedrockClient.ListFoundationModelsAsync(request);

        WriteOutput($"Found {response.ModelSummaries.Count} foundation models");

        response.ShouldNotBeNull();
        response.ModelSummaries.ShouldNotBeEmpty();
        response.ModelSummaries.Any(m => m.ModelId.Contains("claude")).ShouldBeTrue("Should find Claude models");

        WriteOutput("✓ Successfully listed foundation models");
    }

    [Fact]
    public async Task BedrockRuntime_ShouldInvokeClaudeModel_NonStreaming()
    {
        WriteOutput($"Testing Bedrock Runtime - invoking model {_defaultModelId}...");

        var claudeRequest = new
        {
            anthropic_version = "bedrock-2023-05-31",
            max_tokens = _maxTokens,
            messages = new[]
            {
                new { role = "user", content = _testPrompt }
            }
        };

        var body = JsonSerializer.Serialize(claudeRequest);
        var request = new InvokeModelRequest
        {
            ModelId = _defaultModelId,
            Body = new MemoryStream(Encoding.UTF8.GetBytes(body)),
            ContentType = "application/json",
            Accept = "application/json"
        };

        var response = await BedrockRuntimeClient.InvokeModelAsync(request);

        WriteOutput($"Response status: {response.HttpStatusCode}");

        response.ShouldNotBeNull();
        response.HttpStatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        response.Body.ShouldNotBeNull();

        var responseBody = await new StreamReader(response.Body).ReadToEndAsync();
        responseBody.ShouldNotBeNullOrEmpty();

        WriteOutput($"Response length: {responseBody.Length} characters");
        WriteOutput("✓ Successfully invoked Claude model");
    }

    [Fact]
    public async Task BedrockRuntime_ShouldInvokeClaudeModel_Streaming()
    {
        WriteOutput($"Testing Bedrock Runtime - streaming from model {_streamingModelId}...");

        var claudeRequest = new
        {
            anthropic_version = "bedrock-2023-05-31",
            max_tokens = _maxTokens,
            messages = new[]
            {
                new { role = "user", content = _testPrompt }
            }
        };

        var body = JsonSerializer.Serialize(claudeRequest);
        var request = new InvokeModelWithResponseStreamRequest
        {
            ModelId = _streamingModelId,
            Body = new MemoryStream(Encoding.UTF8.GetBytes(body)),
            ContentType = "application/json",
            Accept = "application/json"
        };

        var response = await BedrockRuntimeClient.InvokeModelWithResponseStreamAsync(request);

        WriteOutput($"Response status: {response.HttpStatusCode}");

        response.ShouldNotBeNull();
        response.HttpStatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        response.Body.ShouldNotBeNull();

        var chunkCount = 0;
        var totalLength = 0L;

        await foreach (var streamEvent in response.Body)
        {
            if (streamEvent is PayloadPart payloadPart && payloadPart.Bytes != null)
            {
                chunkCount++;
                totalLength += payloadPart.Bytes.Length;
            }
        }

        WriteOutput($"Received {chunkCount} chunks, total {totalLength} bytes");
        chunkCount.ShouldBeGreaterThan(0, "Should receive streaming chunks");

        WriteOutput("✓ Successfully streamed from Claude model");
    }

    [Fact]
    public async Task BedrockService_ShouldGenerateResponse_WhenConfigured()
    {
        if (BedrockService == null)
        {
            WriteOutput("⚠️ BedrockService not configured, skipping test");
            return;
        }

        WriteOutput("Testing BedrockService - generating response...");

        var request = new ModelInvocationRequest
        {
            ModelId = _defaultModelId,
            Prompt = _testPrompt,
            MaxTokens = _maxTokens,
            Temperature = 0.1,
            TopP = 0.9
        };

        var response = await BedrockService.InvokeModelAsync(request, "functional-test", "1.0");

        WriteOutput($"Generated response length: {response.Content?.Length ?? 0} characters");

        response.ShouldNotBeNull();
        response.Content.ShouldNotBeNullOrEmpty();
        response.Usage.ShouldNotBeNull();
        response.Usage.InputTokens.ShouldBeGreaterThan(0);
        response.Usage.OutputTokens.ShouldBeGreaterThan(0);

        WriteOutput("✓ Successfully generated response via BedrockService");
    }


    [Fact]
    public async Task BedrockService_WithLangfuse_ShouldTraceRequest_WhenBothConfigured()
    {
        if (BedrockService == null)
        {
            WriteOutput("⚠️ BedrockService not configured, skipping test");
            return;
        }

        if (LangfuseService == null)
        {
            WriteOutput("⚠️ LangfuseService not configured, test will run without tracing");
        }
        else
        {
            WriteOutput("✓ Both BedrockService and LangfuseService configured, tracing enabled");
        }

        WriteOutput("Testing BedrockService with Langfuse tracing integration...");

        var request = new ModelInvocationRequest
        {
            ModelId = _defaultModelId,
            Prompt = _testPrompt,
            MaxTokens = _maxTokens,
            Temperature = 0.1,
            TopP = 0.9
        };

        var response = await BedrockService.InvokeModelAsync(request, "langfuse-test", "1.0");

        response.ShouldNotBeNull();
        response.Content.ShouldNotBeNullOrEmpty();

        // Note: We can't easily verify the trace was sent to Langfuse in a functional test
        // without additional infrastructure, but we can verify the request succeeded
        WriteOutput("✓ Request completed successfully with Langfuse integration available");
    }

    [Fact]
    public async Task BedrockRuntime_ShouldHandleInvalidModel_Gracefully()
    {
        WriteOutput("Testing Bedrock Runtime error handling with invalid model...");

        var claudeRequest = new
        {
            anthropic_version = "bedrock-2023-05-31",
            max_tokens = 10,
            messages = new[]
            {
                new { role = "user", content = "test" }
            }
        };

        var body = JsonSerializer.Serialize(claudeRequest);
        var request = new InvokeModelRequest
        {
            ModelId = "invalid-model-id",
            Body = new MemoryStream(Encoding.UTF8.GetBytes(body)),
            ContentType = "application/json",
            Accept = "application/json"
        };

        var exception = await Should.ThrowAsync<Exception>(async () =>
        {
            await BedrockRuntimeClient.InvokeModelAsync(request);
        });

        WriteOutput($"Expected exception caught: {exception.GetType().Name}");
        exception.ShouldNotBeNull();

        WriteOutput("✓ Invalid model handled gracefully with appropriate exception");
    }

    protected override Task CleanupTestResourcesAsync()
    {
        WriteOutput("Bedrock tests cleanup - no resources to clean up");
        return Task.CompletedTask;
    }
}