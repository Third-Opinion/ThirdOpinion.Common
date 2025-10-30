using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.Bedrock;
using Amazon.Bedrock.Model;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using ThirdOpinion.Common.Aws.Bedrock.Configuration;
using ThirdOpinion.Common.Langfuse;
using ThirdOpinion.Common.Logging;
using ThirdOpinion.Common.Misc.RateLimiting;
using ThirdOpinion.Common.Misc.Retry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;

namespace ThirdOpinion.Common.Aws.Bedrock;

/// <summary>
/// Service for invoking AI models via AWS Bedrock runtime
/// </summary>
public class BedrockService : IBedrockService, IDisposable
{
    private readonly IAmazonBedrockRuntime _bedrockRuntimeClient;
    private readonly IAmazonBedrock? _bedrockClient;
    private readonly IRateLimiterService? _rateLimiterService;
    private readonly IRetryPolicyService? _retryPolicyService;
    private readonly ILogger<BedrockService> _logger;
    private readonly ICorrelationIdProvider _correlationIdProvider;
    private readonly BedrockConfig _config;
    private readonly IAsyncPolicy _retryPolicy;
    private readonly ILangfuseService? _langfuseService;
    private readonly IBedrockPricingService _pricingService;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Initializes a new instance of the BedrockService
    /// </summary>
    /// <param name="bedrockRuntimeClient">The AWS Bedrock runtime client</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="correlationIdProvider">Optional correlation ID provider</param>
    /// <param name="langfuseService">Optional Langfuse service for tracing</param>
    /// <param name="pricingService">Optional pricing service for cost calculation</param>
    public BedrockService(
        IAmazonBedrockRuntime bedrockRuntimeClient,
        ILogger<BedrockService> logger,
        ICorrelationIdProvider? correlationIdProvider = null,
        ILangfuseService? langfuseService = null,
        IBedrockPricingService? pricingService = null)
    {
        _bedrockRuntimeClient = bedrockRuntimeClient ?? throw new ArgumentNullException(nameof(bedrockRuntimeClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _correlationIdProvider = correlationIdProvider ?? new SimpleCorrelationIdProvider();
        _langfuseService = langfuseService;
        _pricingService = pricingService ?? new BedrockPricingService();
        _config = new BedrockConfig();
        _bedrockClient = new AmazonBedrockClient();

        _retryPolicy = CreateRetryPolicy();
    }

    /// <inheritdoc />
    public async Task<ModelInvocationResponse> InvokeModelAsync(
        string modelId,
        string prompt,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new ArgumentException("Model ID cannot be null or empty", nameof(modelId));
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("Prompt cannot be null or empty", nameof(prompt));
        }

        var request = new ModelInvocationRequest
        {
            ModelId = modelId,
            Prompt = prompt,
            Parameters = parameters ?? new Dictionary<string, object>()
        };

        return await InvokeModelAsync(request, "unknown", "unknown", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ModelInvocationResponse> InvokeModelAsync(
        ModelInvocationRequest request,
        string promptName,
        string promptVersion,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.ModelId))
        {
            throw new ArgumentException("Model ID cannot be null or empty", nameof(request));
        }

        var correlationId = _correlationIdProvider.GetCorrelationId();
        var stopwatch = Stopwatch.StartNew();
        var startTime = DateTime.UtcNow;

        // Initialize tracing if LangFuse is available
        LangfuseBedrockTraceRequest? trace = null;
        LangfuseGenerationRequest? generation = null;
        string? traceId = null;
        string? generationId = null;

        if (_langfuseService != null)
        {
            traceId = Guid.NewGuid().ToString("N");
            generationId = Guid.NewGuid().ToString("N");

            // Create trace metadata
            var traceMetadata = new BedrockTraceMetadata
            {
                CorrelationId = correlationId,
                Region = Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1",
                Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
            };

            // Create trace and generation
            trace = BedrockTracingExtensions.CreateBedrockTrace(
                traceId,
                $"bedrock-{BedrockTracingExtensions.ExtractModelName(request.ModelId)}",
                traceMetadata,
                request.Prompt ?? request.Messages?.FirstOrDefault()?.Content
            );

            generation = request.ToLangfuseGeneration(traceId, generationId, startTime, promptName, promptVersion, _correlationIdProvider);
        }

        try
        {
            _logger.LogDebug("Invoking Bedrock model: {ModelId} [CorrelationId: {CorrelationId}]",
                request.ModelId, correlationId);

            // Apply rate limiting
            if (_rateLimiterService != null)
            {
                var rateLimiter = _rateLimiterService.GetRateLimiter("Bedrock");
                await rateLimiter.WaitAsync(cancellationToken);
            }

            // Prepare the request body based on model type
            var requestBody = PrepareRequestBody(request);

            var invokeRequest = new InvokeModelRequest
            {
                ModelId = request.ModelId,
                Body = new MemoryStream(Encoding.UTF8.GetBytes(requestBody)),
                ContentType = "application/json",
                Accept = "application/json"
            };

            // Execute with retry policy
            var response = await _retryPolicy.ExecuteAsync(async () =>
            {
                return await _bedrockRuntimeClient.InvokeModelAsync(invokeRequest, cancellationToken);
            });

            stopwatch.Stop();

            // Parse the response based on model type
            var result = await ParseResponseAsync(response, request.ModelId, stopwatch.Elapsed);

            // Update tracing with successful response
            if (_langfuseService != null && generation != null && trace != null)
            {
                try
                {
                    // Calculate costs
                    var cost = _pricingService.CalculateCost(
                        request.ModelId,
                        result.Usage.InputTokens,
                        result.Usage.OutputTokens
                    );

                    // Update generation with response data
                    generation.WithResponse(result, cost);
                    trace.WithOutput(result.Content, success: true);

                    // Send to LangFuse
                    var batch = BedrockTracingExtensions.CreateIngestionBatch(trace, generation);
                    await _langfuseService.SendIngestionBatchAsync(batch, cancellationToken);

                    _logger.LogDebug("Successfully sent Bedrock tracing data to LangFuse [CorrelationId: {CorrelationId}]", correlationId);
                }
                catch (Exception tracingEx)
                {
                    _logger.LogWarning(tracingEx, "Failed to send tracing data to LangFuse [CorrelationId: {CorrelationId}]", correlationId);
                }
            }

            _logger.LogInformation("Successfully invoked Bedrock model: {ModelId}, Duration: {Duration}ms, " +
                                 "InputTokens: {InputTokens}, OutputTokens: {OutputTokens} [CorrelationId: {CorrelationId}]",
                request.ModelId, stopwatch.ElapsedMilliseconds, result.Usage.InputTokens,
                result.Usage.OutputTokens, correlationId);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Update tracing with error
            if (_langfuseService != null && generation != null && trace != null)
            {
                try
                {
                    generation.WithError(ex);
                    trace.WithOutput(null, success: false, errorMessage: ex.Message);

                    var batch = BedrockTracingExtensions.CreateIngestionBatch(trace, generation);
                    await _langfuseService.SendIngestionBatchAsync(batch, cancellationToken);

                    _logger.LogDebug("Successfully sent error tracing data to LangFuse [CorrelationId: {CorrelationId}]", correlationId);
                }
                catch (Exception tracingEx)
                {
                    _logger.LogWarning(tracingEx, "Failed to send error tracing data to LangFuse [CorrelationId: {CorrelationId}]", correlationId);
                }
            }

            _logger.LogError(ex, "Failed to invoke Bedrock model: {ModelId}, Duration: {Duration}ms [CorrelationId: {CorrelationId}]",
                request.ModelId, stopwatch.ElapsedMilliseconds, correlationId);

            throw new BedrockServiceException(
                $"Failed to invoke Bedrock model '{request.ModelId}': {ex.Message}",
                ex);
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<string>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
    {
        var correlationId = _correlationIdProvider.GetCorrelationId();

        try
        {
            _logger.LogDebug("Retrieving available Bedrock models (Claude 4.x only) [CorrelationId: {CorrelationId}]", correlationId);

            // Get foundation models dynamically
            var foundationModels = await GetFoundationModelsAsync(includeUnsupportedClaude3: false, cancellationToken: cancellationToken);

            // Extract model IDs, prioritizing Claude 4.x models
            var availableModels = foundationModels
                .Where(m => BedrockModels.IsClaude4Model(m.ModelId) || !BedrockModels.IsClaudeModel(m.ModelId))
                .OrderBy(m => BedrockModels.IsClaudeModel(m.ModelId) ? 0 : 1) // Claude models first
                .ThenBy(m => m.ModelId)
                .Select(m => m.ModelId)
                .ToList();

            // Fallback to known Claude 4.x models if dynamic discovery fails
            if (!availableModels.Any())
            {
                _logger.LogWarning("Dynamic model discovery returned no models, falling back to known Claude 4.x models [CorrelationId: {CorrelationId}]", correlationId);
                availableModels = new[]
                {
                    BedrockModels.Claude.Claude4SonnetLatest,
                    BedrockModels.Claude.Claude4HaikuLatest,
                    BedrockModels.Claude.Claude4OpusLatest
                }.ToList();
            }

            _logger.LogDebug("Retrieved {ModelCount} available Bedrock models [CorrelationId: {CorrelationId}]",
                availableModels.Count, correlationId);

            return availableModels;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve available Bedrock models [CorrelationId: {CorrelationId}]",
                correlationId);
            throw new BedrockServiceException($"Failed to retrieve available models: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<bool> TestConnectivityAsync(CancellationToken cancellationToken = default)
    {
        var correlationId = _correlationIdProvider.GetCorrelationId();

        try
        {
            _logger.LogDebug("Testing Bedrock connectivity [CorrelationId: {CorrelationId}]", correlationId);

            // Use a simple model invocation with minimal prompt to test connectivity
            var testRequest = new ModelInvocationRequest
            {
                ModelId = BedrockModels.Claude.Claude4HaikuLatest, // Use fastest/cheapest Claude 4.x model for testing
                Prompt = "Hello",
                MaxTokens = 1,
                Temperature = 0.0
            };

            await InvokeModelAsync(testRequest, "connectivity-test", "1", cancellationToken);

            _logger.LogInformation("Bedrock connectivity test successful [CorrelationId: {CorrelationId}]", correlationId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Bedrock connectivity test failed [CorrelationId: {CorrelationId}]", correlationId);
            return false;
        }
    }

    private string PrepareRequestBody(ModelInvocationRequest request)
    {
        if (BedrockModels.IsClaudeModel(request.ModelId))
        {
            return PrepareClaudeRequest(request);
        }
        else if (BedrockModels.IsTitanModel(request.ModelId))
        {
            return PrepareTitanRequest(request);
        }
        else
        {
            // Default to Claude format for unknown models
            return PrepareClaudeRequest(request);
        }
    }

    private string PrepareClaudeRequest(ModelInvocationRequest request)
    {
        var claudeRequest = new ClaudeRequest
        {
            MaxTokens = request.MaxTokens ?? _config.DefaultMaxTokens,
            Temperature = request.Temperature ?? _config.DefaultTemperature,
            TopP = request.TopP,
            System = request.SystemPrompt,
            StopSequences = request.StopSequences
        };

        // Add tool configuration if available
        if (request.ToolConfiguration != null)
        {
            claudeRequest.Tools = ToolConfigurationHelper.ConvertToClaudeTools(request.ToolConfiguration);

            // Set tool choice to "any" to force tool use when tools are provided
            if (claudeRequest.Tools?.Any() == true)
            {
                claudeRequest.ToolChoice = new ClaudeToolChoice { Type = "any" };

                _logger.LogDebug("Added {ToolCount} tools to Claude request", claudeRequest.Tools.Count);
            }
        }

        if (request.Messages?.Any() == true)
        {
            claudeRequest.Messages = request.Messages.Select(m => new ClaudeMessage
            {
                Role = m.Role,
                Content = m.Content
            }).ToList();
        }
        else
        {
            claudeRequest.Messages = new List<ClaudeMessage>
            {
                new() { Role = "user", Content = request.Prompt }
            };
        }

        return JsonSerializer.Serialize(claudeRequest, JsonOptions);
    }

    private string PrepareTitanRequest(ModelInvocationRequest request)
    {
        var titanRequest = new TitanTextRequest
        {
            InputText = request.Prompt,
            TextGenerationConfig = new TitanTextGenerationConfig
            {
                MaxTokenCount = request.MaxTokens ?? _config.DefaultMaxTokens,
                Temperature = request.Temperature ?? _config.DefaultTemperature,
                TopP = request.TopP ?? 1.0,
                StopSequences = request.StopSequences
            }
        };

        return JsonSerializer.Serialize(titanRequest, JsonOptions);
    }

    private async Task<ModelInvocationResponse> ParseResponseAsync(
        InvokeModelResponse response,
        string modelId,
        TimeSpan duration)
    {
        var responseBody = await new StreamReader(response.Body).ReadToEndAsync();

        var result = new ModelInvocationResponse
        {
            ModelId = modelId,
            Duration = duration,
            RawResponse = responseBody,
            RequestId = response.ResponseMetadata?.RequestId
        };

        if (BedrockModels.IsClaudeModel(modelId))
        {
            ParseClaudeResponse(responseBody, result);
        }
        else if (BedrockModels.IsTitanModel(modelId))
        {
            ParseTitanResponse(responseBody, result);
        }
        else
        {
            // Default to Claude parsing for unknown models
            ParseClaudeResponse(responseBody, result);
        }

        return result;
    }

    private void ParseClaudeResponse(string responseBody, ModelInvocationResponse result)
    {
        try
        {
            var claudeResponse = JsonSerializer.Deserialize<ClaudeResponse>(responseBody, JsonOptions);
            if (claudeResponse != null)
            {
                result.Content = string.Join("", claudeResponse.Content.Select(c => c.Text));
                result.StopReason = claudeResponse.StopReason;
                result.Usage = new ModelUsage
                {
                    InputTokens = claudeResponse.Usage.InputTokens,
                    OutputTokens = claudeResponse.Usage.OutputTokens
                };
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse Claude response, using raw content");
            result.Content = responseBody;
        }
    }

    private void ParseTitanResponse(string responseBody, ModelInvocationResponse result)
    {
        try
        {
            var titanResponse = JsonSerializer.Deserialize<TitanTextResponse>(responseBody, JsonOptions);
            if (titanResponse?.Results?.Any() == true)
            {
                var firstResult = titanResponse.Results.First();
                result.Content = firstResult.OutputText;
                result.StopReason = firstResult.CompletionReason;
                result.Usage = new ModelUsage
                {
                    InputTokens = titanResponse.InputTextTokenCount,
                    OutputTokens = firstResult.TokenCount
                };
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse Titan response, using raw content");
            result.Content = responseBody;
        }
    }

    private IAsyncPolicy CreateRetryPolicy()
    {
        return Policy
            .Handle<AmazonBedrockRuntimeException>(ex => IsTransientError(ex))
            .Or<HttpRequestException>()
            .Or<TaskCanceledException>()
            .Or<TimeoutException>()
            .WaitAndRetryAsync(
                retryCount: _config.MaxRetries,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) +
                    TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000)),
                onRetry: (exception, timespan, retryCount, context) =>
                {
                    var correlationId = _correlationIdProvider.GetCorrelationId();
                    _logger.LogWarning(exception,
                        "Retry attempt {RetryCount} for Bedrock operation. Waiting {Delay}ms before next retry. " +
                        "[CorrelationId: {CorrelationId}]",
                        retryCount, timespan.TotalMilliseconds, correlationId);
                });
    }

    private static bool IsTransientError(Exception exception)
    {
        if (exception is AmazonBedrockRuntimeException bedrockEx)
        {
            return bedrockEx.ErrorCode switch
            {
                "ThrottlingException" => true,
                "ServiceUnavailableException" => true,
                "InternalServerException" => true,
                "ModelTimeoutException" => true,
                _ when bedrockEx.StatusCode == System.Net.HttpStatusCode.InternalServerError => true,
                _ when bedrockEx.StatusCode == System.Net.HttpStatusCode.BadGateway => true,
                _ when bedrockEx.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable => true,
                _ when bedrockEx.StatusCode == System.Net.HttpStatusCode.GatewayTimeout => true,
                _ when bedrockEx.StatusCode == System.Net.HttpStatusCode.TooManyRequests => true,
                _ => false
            };
        }

        return exception is TimeoutException or TaskCanceledException or HttpRequestException;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BedrockFoundationModel>> GetFoundationModelsAsync(
        string? byProvider = null,
        bool includeUnsupportedClaude3 = false,
        CancellationToken cancellationToken = default)
    {
        var correlationId = _correlationIdProvider.GetCorrelationId();

        try
        {
            _logger.LogDebug("Retrieving foundation models from Bedrock [CorrelationId: {CorrelationId}]", correlationId);

            var request = new Amazon.Bedrock.Model.ListFoundationModelsRequest();
            if (!string.IsNullOrWhiteSpace(byProvider))
            {
                request.ByProvider = byProvider;
            }

            var response = await _bedrockClient.ListFoundationModelsAsync(request, cancellationToken);
            var models = new List<BedrockFoundationModel>();

            foreach (var model in response.ModelSummaries)
            {
                // Skip unsupported Claude 3.x models unless explicitly requested
                if (!includeUnsupportedClaude3 && BedrockModels.IsUnsupportedClaude3Model(model.ModelId))
                {
                    continue;
                }

                models.Add(new BedrockFoundationModel
                {
                    ModelId = model.ModelId,
                    ModelName = model.ModelName ?? string.Empty,
                    ProviderName = model.ProviderName ?? string.Empty,
                    ModelArn = model.ModelArn ?? string.Empty,
                    InputModalities = model.InputModalities?.ToList() ?? new List<string>(),
                    OutputModalities = model.OutputModalities?.ToList() ?? new List<string>(),
                    ResponseStreamingSupported = model.ResponseStreamingSupported ?? false,
                    Customizations = new List<string>(), // Not available in FoundationModelSummary
                    InferenceTypes = new List<string>() // Not available in FoundationModelSummary
                });
            }

            _logger.LogDebug("Retrieved {ModelCount} foundation models from Bedrock [CorrelationId: {CorrelationId}]",
                models.Count, correlationId);

            return models;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve foundation models from Bedrock [CorrelationId: {CorrelationId}]",
                correlationId);
            throw new BedrockServiceException($"Failed to retrieve foundation models: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<BedrockFoundationModel?> GetModelInfoAsync(string modelId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new ArgumentException("Model ID cannot be null or empty", nameof(modelId));
        }

        var correlationId = _correlationIdProvider.GetCorrelationId();

        try
        {
            _logger.LogDebug("Retrieving model info for {ModelId} [CorrelationId: {CorrelationId}]", modelId, correlationId);

            var request = new Amazon.Bedrock.Model.GetFoundationModelRequest
            {
                ModelIdentifier = modelId
            };

            var response = await _bedrockClient.GetFoundationModelAsync(request, cancellationToken);
            var model = response.ModelDetails;

            if (model == null)
            {
                return null;
            }

            var result = new BedrockFoundationModel
            {
                ModelId = model.ModelId,
                ModelName = model.ModelName ?? string.Empty,
                ProviderName = model.ProviderName ?? string.Empty,
                ModelArn = model.ModelArn ?? string.Empty,
                InputModalities = model.InputModalities?.ToList() ?? new List<string>(),
                OutputModalities = model.OutputModalities?.ToList() ?? new List<string>(),
                ResponseStreamingSupported = model.ResponseStreamingSupported ?? false,
                Customizations = new List<string>(), // Not available in FoundationModelDetails
                InferenceTypes = new List<string>() // Not available in FoundationModelDetails
            };

            _logger.LogDebug("Retrieved model info for {ModelId} [CorrelationId: {CorrelationId}]", modelId, correlationId);

            return result;
        }
        catch (Amazon.Bedrock.Model.ResourceNotFoundException)
        {
            _logger.LogWarning("Model {ModelId} not found [CorrelationId: {CorrelationId}]", modelId, correlationId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve model info for {ModelId} [CorrelationId: {CorrelationId}]",
                modelId, correlationId);
            throw new BedrockServiceException($"Failed to retrieve model info for '{modelId}': {ex.Message}", ex);
        }
    }

    public void Dispose()
    {
        _bedrockRuntimeClient?.Dispose();
        _bedrockClient?.Dispose();
    }
}

/// <summary>
/// Exception thrown by BedrockService operations
/// </summary>
public class BedrockServiceException : Exception
{
    public BedrockServiceException(string message) : base(message) { }
    public BedrockServiceException(string message, Exception innerException) : base(message, innerException) { }
}