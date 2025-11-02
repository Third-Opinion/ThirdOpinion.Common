using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Amazon.Bedrock;
using Amazon.Bedrock.Model;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using ThirdOpinion.Common.Aws.Bedrock.Configuration;
using ThirdOpinion.Common.Langfuse;
using ThirdOpinion.Common.Logging;
using ThirdOpinion.Common.Misc.RateLimiting;
using ThirdOpinion.Common.Misc.Retry;
using ResourceNotFoundException = Amazon.Bedrock.Model.ResourceNotFoundException;

namespace ThirdOpinion.Common.Aws.Bedrock;

/// <summary>
///     Service for invoking AI models via AWS Bedrock runtime
/// </summary>
public class BedrockService : IBedrockService, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IAmazonBedrock _bedrockClient;
    private readonly IAmazonBedrockRuntime _bedrockRuntimeClient;
    private readonly BedrockConfig _config;
    private readonly ICorrelationIdProvider _correlationIdProvider;
    private readonly ILangfuseService? _langfuseService;
    private readonly ILogger<BedrockService> _logger;
    private readonly IBedrockPricingService _pricingService;
    private readonly IRateLimiterService _rateLimiterService;
    private readonly IAsyncPolicy _retryPolicy;
    private readonly IRetryPolicyService _retryPolicyService;

    /// <summary>
    ///     Initializes a new instance of the BedrockService
    /// </summary>
    /// <param name="bedrockRuntimeClient">The AWS Bedrock runtime client</param>
    /// <param name="bedrockClient">The AWS Bedrock client</param>
    /// <param name="rateLimiterService">Rate limiter service</param>
    /// <param name="retryPolicyService">Retry policy service</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="correlationIdProvider">Correlation ID provider</param>
    /// <param name="config">Bedrock configuration</param>
    /// <param name="langfuseService">Optional Langfuse service for tracing</param>
    /// <param name="pricingService">Optional pricing service for cost calculation</param>
    public BedrockService(
        IAmazonBedrockRuntime bedrockRuntimeClient,
        IAmazonBedrock bedrockClient,
        IRateLimiterService rateLimiterService,
        IRetryPolicyService retryPolicyService,
        ILogger<BedrockService> logger,
        ICorrelationIdProvider correlationIdProvider,
        IOptions<BedrockConfig> config,
        ILangfuseService? langfuseService = null,
        IBedrockPricingService? pricingService = null)
    {
        _bedrockRuntimeClient = bedrockRuntimeClient ?? throw new ArgumentNullException(nameof(bedrockRuntimeClient));
        _bedrockClient = bedrockClient ?? throw new ArgumentNullException(nameof(bedrockClient));
        _rateLimiterService = rateLimiterService ?? throw new ArgumentNullException(nameof(rateLimiterService));
        _retryPolicyService = retryPolicyService ?? throw new ArgumentNullException(nameof(retryPolicyService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _correlationIdProvider = correlationIdProvider ?? throw new ArgumentNullException(nameof(correlationIdProvider));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _langfuseService = langfuseService;
        _pricingService = pricingService ?? new BedrockPricingService();

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
            throw new ArgumentException("Model ID cannot be null or empty", nameof(modelId));

        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Prompt cannot be null or empty", nameof(prompt));

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
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.ModelId))
            throw new ArgumentException("Model ID cannot be null or empty", nameof(request));

        string correlationId = _correlationIdProvider.GetCorrelationId();
        var stopwatch = Stopwatch.StartNew();
        DateTime startTime = DateTime.UtcNow;

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
                Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ??
                              "Production"
            };

            // Create trace and generation
            trace = BedrockTracingExtensions.CreateBedrockTrace(
                traceId,
                $"bedrock-{BedrockTracingExtensions.ExtractModelName(request.ModelId)}",
                traceMetadata,
                request.Prompt ?? request.Messages?.FirstOrDefault()?.Content
            );

            generation = request.ToLangfuseGeneration(traceId, generationId, startTime, promptName,
                promptVersion, _correlationIdProvider);
        }

        try
        {
            _logger.LogDebug("Invoking Bedrock model: {ModelId} [CorrelationId: {CorrelationId}]",
                request.ModelId, correlationId);

            // Apply rate limiting
            if (_rateLimiterService != null)
            {
                IRateLimiter rateLimiter = _rateLimiterService.GetRateLimiter("Bedrock");
                await rateLimiter.WaitAsync(cancellationToken);
            }

            // Prepare the request body based on model type
            string requestBody = PrepareRequestBody(request);

            var invokeRequest = new InvokeModelRequest
            {
                ModelId = request.ModelId,
                Body = new MemoryStream(Encoding.UTF8.GetBytes(requestBody)),
                ContentType = "application/json",
                Accept = "application/json"
            };

            // Execute with retry policy
            InvokeModelResponse? response = await _retryPolicy.ExecuteAsync(async () =>
            {
                return await _bedrockRuntimeClient.InvokeModelAsync(invokeRequest,
                    cancellationToken);
            });

            stopwatch.Stop();

            // Parse the response based on model type
            ModelInvocationResponse result
                = await ParseResponseAsync(response, request.ModelId, stopwatch.Elapsed);

            // Update tracing with successful response
            if (_langfuseService != null && generation != null && trace != null)
                try
                {
                    // Calculate costs
                    BedrockCostCalculation cost = _pricingService.CalculateCost(
                        request.ModelId,
                        result.Usage.InputTokens,
                        result.Usage.OutputTokens
                    );

                    // Update generation with response data
                    generation.WithResponse(result, cost);
                    trace.WithOutput(result.Content);

                    // Send to LangFuse
                    LangfuseIngestionBatch batch
                        = BedrockTracingExtensions.CreateIngestionBatch(trace, generation);
                    await _langfuseService.SendIngestionBatchAsync(batch, cancellationToken);

                    _logger.LogDebug(
                        "Successfully sent Bedrock tracing data to LangFuse [CorrelationId: {CorrelationId}]",
                        correlationId);
                }
                catch (Exception tracingEx)
                {
                    _logger.LogWarning(tracingEx,
                        "Failed to send tracing data to LangFuse [CorrelationId: {CorrelationId}]",
                        correlationId);
                }

            _logger.LogInformation(
                "Successfully invoked Bedrock model: {ModelId}, Duration: {Duration}ms, " +
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
                try
                {
                    generation.WithError(ex);
                    trace.WithOutput(null, false, ex.Message);

                    LangfuseIngestionBatch batch
                        = BedrockTracingExtensions.CreateIngestionBatch(trace, generation);
                    await _langfuseService.SendIngestionBatchAsync(batch, cancellationToken);

                    _logger.LogDebug(
                        "Successfully sent error tracing data to LangFuse [CorrelationId: {CorrelationId}]",
                        correlationId);
                }
                catch (Exception tracingEx)
                {
                    _logger.LogWarning(tracingEx,
                        "Failed to send error tracing data to LangFuse [CorrelationId: {CorrelationId}]",
                        correlationId);
                }

            _logger.LogError(ex,
                "Failed to invoke Bedrock model: {ModelId}, Duration: {Duration}ms [CorrelationId: {CorrelationId}]",
                request.ModelId, stopwatch.ElapsedMilliseconds, correlationId);

            throw new BedrockServiceException(
                $"Failed to invoke Bedrock model '{request.ModelId}': {ex.Message}",
                ex);
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<string>> GetAvailableModelsAsync(
        CancellationToken cancellationToken = default)
    {
        string correlationId = _correlationIdProvider.GetCorrelationId();

        try
        {
            _logger.LogDebug(
                "Retrieving available Bedrock models (Claude 4.x only) [CorrelationId: {CorrelationId}]",
                correlationId);

            // Get foundation models dynamically
            IEnumerable<BedrockFoundationModel> foundationModels
                = await GetFoundationModelsAsync(includeUnsupportedClaude3: false,
                    cancellationToken: cancellationToken);

            // Extract model IDs, prioritizing Claude 4.x models
            List<string> availableModels = foundationModels
                .Where(m =>
                    BedrockModels.IsClaude4Model(m.ModelId) ||
                    !BedrockModels.IsClaudeModel(m.ModelId))
                .OrderBy(m => BedrockModels.IsClaudeModel(m.ModelId) ? 0 : 1) // Claude models first
                .ThenBy(m => m.ModelId)
                .Select(m => m.ModelId)
                .ToList();

            // Fallback to known Claude 4.x models if dynamic discovery fails
            if (!availableModels.Any())
            {
                _logger.LogWarning(
                    "Dynamic model discovery returned no models, falling back to known Claude 4.x models [CorrelationId: {CorrelationId}]",
                    correlationId);
                availableModels = new[]
                {
                    BedrockModels.Claude.Claude4SonnetLatest,
                    BedrockModels.Claude.Claude4HaikuLatest,
                    BedrockModels.Claude.Claude4OpusLatest
                }.ToList();
            }

            _logger.LogDebug(
                "Retrieved {ModelCount} available Bedrock models [CorrelationId: {CorrelationId}]",
                availableModels.Count, correlationId);

            return availableModels;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to retrieve available Bedrock models [CorrelationId: {CorrelationId}]",
                correlationId);
            throw new BedrockServiceException($"Failed to retrieve available models: {ex.Message}",
                ex);
        }
    }

    /// <inheritdoc />
    public async Task<bool> TestConnectivityAsync(CancellationToken cancellationToken = default)
    {
        string correlationId = _correlationIdProvider.GetCorrelationId();

        try
        {
            _logger.LogDebug("Testing Bedrock connectivity [CorrelationId: {CorrelationId}]",
                correlationId);

            // Use a simple model invocation with minimal prompt to test connectivity
            var testRequest = new ModelInvocationRequest
            {
                ModelId = BedrockModels.Claude
                    .Claude4HaikuLatest, // Use fastest/cheapest Claude 4.x model for testing
                Prompt = "Hello",
                MaxTokens = 1,
                Temperature = 0.0
            };

            await InvokeModelAsync(testRequest, "connectivity-test", "1", cancellationToken);

            _logger.LogInformation(
                "Bedrock connectivity test successful [CorrelationId: {CorrelationId}]",
                correlationId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Bedrock connectivity test failed [CorrelationId: {CorrelationId}]", correlationId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BedrockFoundationModel>> GetFoundationModelsAsync(
        string? byProvider = null,
        bool includeUnsupportedClaude3 = false,
        CancellationToken cancellationToken = default)
    {
        string correlationId = _correlationIdProvider.GetCorrelationId();

        try
        {
            _logger.LogDebug(
                "Retrieving foundation models from Bedrock [CorrelationId: {CorrelationId}]",
                correlationId);

            var request = new ListFoundationModelsRequest();
            if (!string.IsNullOrWhiteSpace(byProvider)) request.ByProvider = byProvider;

            Amazon.Bedrock.Model.ListFoundationModelsResponse? response
                = await _bedrockClient!.ListFoundationModelsAsync(request, cancellationToken);
            var models = new List<BedrockFoundationModel>();

            foreach (FoundationModelSummary? model in response.ModelSummaries)
            {
                // Skip unsupported Claude 3.x models unless explicitly requested
                if (!includeUnsupportedClaude3 &&
                    BedrockModels.IsUnsupportedClaude3Model(model.ModelId)) continue;

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

            _logger.LogDebug(
                "Retrieved {ModelCount} foundation models from Bedrock [CorrelationId: {CorrelationId}]",
                models.Count, correlationId);

            return models;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to retrieve foundation models from Bedrock [CorrelationId: {CorrelationId}]",
                correlationId);
            throw new BedrockServiceException($"Failed to retrieve foundation models: {ex.Message}",
                ex);
        }
    }

    /// <inheritdoc />
    public async Task<BedrockFoundationModel?> GetModelInfoAsync(string modelId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelId))
            throw new ArgumentException("Model ID cannot be null or empty", nameof(modelId));

        string correlationId = _correlationIdProvider.GetCorrelationId();

        try
        {
            _logger.LogDebug("Retrieving model info for {ModelId} [CorrelationId: {CorrelationId}]",
                modelId, correlationId);

            var request = new GetFoundationModelRequest
            {
                ModelIdentifier = modelId
            };

            GetFoundationModelResponse? response
                = await _bedrockClient!.GetFoundationModelAsync(request, cancellationToken);
            FoundationModelDetails? model = response.ModelDetails;

            if (model == null) return null;

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

            _logger.LogDebug("Retrieved model info for {ModelId} [CorrelationId: {CorrelationId}]",
                modelId, correlationId);

            return result;
        }
        catch (ResourceNotFoundException)
        {
            _logger.LogWarning("Model {ModelId} not found [CorrelationId: {CorrelationId}]",
                modelId, correlationId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to retrieve model info for {ModelId} [CorrelationId: {CorrelationId}]",
                modelId, correlationId);
            throw new BedrockServiceException(
                $"Failed to retrieve model info for '{modelId}': {ex.Message}", ex);
        }
    }

    public void Dispose()
    {
        _bedrockRuntimeClient?.Dispose();
        _bedrockClient?.Dispose();
    }

    private string PrepareRequestBody(ModelInvocationRequest request)
    {
        if (BedrockModels.IsClaudeModel(request.ModelId)) return PrepareClaudeRequest(request);

        if (BedrockModels.IsTitanModel(request.ModelId)) return PrepareTitanRequest(request);

        // Default to Claude format for unknown models
        return PrepareClaudeRequest(request);
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
            claudeRequest.Tools
                = ToolConfigurationHelper.ConvertToClaudeTools(request.ToolConfiguration);

            // Set tool choice to "any" to force tool use when tools are provided
            if (claudeRequest.Tools?.Any() == true)
            {
                claudeRequest.ToolChoice = new ClaudeToolChoice { Type = "any" };

                _logger.LogDebug("Added {ToolCount} tools to Claude request",
                    claudeRequest.Tools.Count);
            }
        }

        if (request.Messages?.Any() == true)
            claudeRequest.Messages = request.Messages.Select(m => new ClaudeMessage
            {
                Role = m.Role,
                Content = m.Content
            }).ToList();
        else
            claudeRequest.Messages = new List<ClaudeMessage>
            {
                new() { Role = "user", Content = request.Prompt }
            };

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
        string responseBody = await new StreamReader(response.Body).ReadToEndAsync();

        var result = new ModelInvocationResponse
        {
            ModelId = modelId,
            Duration = duration,
            RawResponse = responseBody,
            RequestId = response.ResponseMetadata?.RequestId
        };

        if (BedrockModels.IsClaudeModel(modelId))
            ParseClaudeResponse(responseBody, result);
        else if (BedrockModels.IsTitanModel(modelId))
            ParseTitanResponse(responseBody, result);
        else
            // Default to Claude parsing for unknown models
            ParseClaudeResponse(responseBody, result);

        return result;
    }

    private void ParseClaudeResponse(string responseBody, ModelInvocationResponse result)
    {
        try
        {
            var claudeResponse
                = JsonSerializer.Deserialize<ClaudeResponse>(responseBody, JsonOptions);
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
            var titanResponse
                = JsonSerializer.Deserialize<TitanTextResponse>(responseBody, JsonOptions);
            if (titanResponse?.Results?.Any() == true)
            {
                TitanResult firstResult = titanResponse.Results.First();
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
                _config.MaxRetries,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) +
                                TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000)),
                (exception, timespan, retryCount, context) =>
                {
                    string correlationId = _correlationIdProvider.GetCorrelationId();
                    _logger.LogWarning(exception,
                        "Retry attempt {RetryCount} for Bedrock operation. Waiting {Delay}ms before next retry. " +
                        "[CorrelationId: {CorrelationId}]",
                        retryCount, timespan.TotalMilliseconds, correlationId);
                });
    }

    private static bool IsTransientError(Exception exception)
    {
        if (exception is AmazonBedrockRuntimeException bedrockEx)
            return bedrockEx.ErrorCode switch
            {
                "ThrottlingException" => true,
                "ServiceUnavailableException" => true,
                "InternalServerException" => true,
                "ModelTimeoutException" => true,
                _ when bedrockEx.StatusCode == HttpStatusCode.InternalServerError => true,
                _ when bedrockEx.StatusCode == HttpStatusCode.BadGateway => true,
                _ when bedrockEx.StatusCode == HttpStatusCode.ServiceUnavailable => true,
                _ when bedrockEx.StatusCode == HttpStatusCode.GatewayTimeout => true,
                _ when bedrockEx.StatusCode == HttpStatusCode.TooManyRequests => true,
                _ => false
            };

        return exception is TimeoutException or TaskCanceledException or HttpRequestException;
    }

    /// <summary>
    ///     Substitutes variables in a prompt template using values from an LlmRecord
    /// </summary>
    /// <param name="promptTemplate">The prompt template with {{variable}} placeholders</param>
    /// <param name="record">The LlmRecord containing variable values</param>
    /// <returns>The prompt with all variables substituted</returns>
    private string SubstitutePromptVariables(string promptTemplate, LlmRecord record)
    {
        string correlationId = _correlationIdProvider.GetCorrelationId();
        Dictionary<string, string> metadata = record.ParseMetadata();

        // Extract all variables to substitute
        List<string> variables = ExtractPromptVariables(promptTemplate);
        string prompt = promptTemplate;

        _logger.LogDebug("Substituting {VariableCount} variables in prompt: {Variables} [CorrelationId: {CorrelationId}]",
            variables.Count, string.Join(", ", variables), correlationId);

        // Replace each variable with its value
        foreach (string variable in variables)
        {
            string value = GetVariableValue(variable, record, metadata);
            string placeholder = $"{{{{{variable}}}}}";

            // Use case-insensitive replacement
            prompt = ReplaceIgnoreCase(prompt, placeholder, value);

            _logger.LogDebug("Substituted variable '{Variable}' with value of length {ValueLength} [CorrelationId: {CorrelationId}]",
                variable, value.Length, correlationId);
        }

        return prompt;
    }

    /// <summary>
    ///     Substitutes variables in a prompt template using values from a provided dictionary
    /// </summary>
    /// <param name="promptTemplate">The prompt template with {{variable}} placeholders</param>
    /// <param name="variableValues">Dictionary of variable names to values</param>
    /// <returns>The prompt with all variables substituted</returns>
    public string SubstitutePromptVariables(string promptTemplate, Dictionary<string, string> variableValues)
    {
        string correlationId = _correlationIdProvider.GetCorrelationId();

        // Extract all variables to substitute
        List<string> variables = ExtractPromptVariables(promptTemplate);
        string prompt = promptTemplate;

        _logger.LogDebug("Substituting {VariableCount} variables in prompt: {Variables} [CorrelationId: {CorrelationId}]",
            variables.Count, string.Join(", ", variables), correlationId);

        // Replace each variable with its value
        foreach (string variable in variables)
        {
            // Try case-sensitive lookup first, then case-insensitive
            if (!variableValues.TryGetValue(variable, out string? value))
            {
                string? key = variableValues.Keys.FirstOrDefault(k =>
                    string.Equals(k, variable, StringComparison.OrdinalIgnoreCase));

                value = key != null ? variableValues[key] : "";
            }

            string placeholder = $"{{{{{variable}}}}}";

            // Use case-insensitive replacement
            prompt = ReplaceIgnoreCase(prompt, placeholder, value);

            _logger.LogDebug("Substituted variable '{Variable}' with value of length {ValueLength} [CorrelationId: {CorrelationId}]",
                variable, value.Length, correlationId);
        }

        return prompt;
    }

    /// <summary>
    ///     Extracts variable names from a prompt template
    /// </summary>
    private static List<string> ExtractPromptVariables(string promptTemplate)
    {
        if (string.IsNullOrWhiteSpace(promptTemplate))
            return new List<string>();

        string pattern = @"\{\{\s*([a-zA-Z][a-zA-Z0-9_]*)\s*\}\}";
        Regex regex = new(pattern, RegexOptions.IgnoreCase);

        var variables = new List<string>();
        MatchCollection matches = regex.Matches(promptTemplate);

        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1)
            {
                string variableName = match.Groups[1].Value.Trim();
                if (!variables.Contains(variableName, StringComparer.OrdinalIgnoreCase))
                {
                    variables.Add(variableName);
                }
            }
        }

        return variables;
    }

    /// <summary>
    ///     Gets the value for a variable from the record or metadata
    /// </summary>
    private static string GetVariableValue(string variableName, LlmRecord record, Dictionary<string, string> metadata)
    {
        // Check built-in record field variables
        string? value = variableName.ToLowerInvariant() switch
        {
            "practice_id" or "practiceid" => record.PracticeId,
            "patient_id" or "patientid" => record.PatientId,
            _ => null
        };

        if (value != null)
            return value;

        // Check metadata (case-sensitive lookup first, then case-insensitive)
        if (metadata.TryGetValue(variableName, out string? metadataValue))
            return metadataValue;

        // Try case-insensitive lookup for metadata
        string? metadataKey = metadata.Keys.FirstOrDefault(k =>
            string.Equals(k, variableName, StringComparison.OrdinalIgnoreCase));

        if (metadataKey != null)
            return metadata[metadataKey];

        return "";
    }

    /// <summary>
    ///     Replaces a string with another string (case-insensitive)
    /// </summary>
    private static string ReplaceIgnoreCase(string source, string oldValue, string newValue)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(oldValue))
            return source;

        string pattern = Regex.Escape(oldValue);
        return Regex.Replace(source, pattern, newValue, RegexOptions.IgnoreCase);
    }

    /// <summary>
    ///     Extracts configuration values from a Langfuse config dictionary
    /// </summary>
    private (string? Model, double? Temperature, double? TopP, int? MaxTokens) ExtractLangfuseConfig(Dictionary<string, object>? config)
    {
        if (config == null)
            return (null, null, null, null);

        string? model = null;
        double? temperature = null;
        double? topP = null;
        int? maxTokens = null;

        if (config.TryGetValue("model", out object? modelValue) && modelValue is string modelStr)
        {
            model = modelStr;
        }

        if (config.TryGetValue("temperature", out object? tempValue))
        {
            temperature = Convert.ToDouble(tempValue);
        }

        if (config.TryGetValue("top_p", out object? topPValue))
        {
            topP = Convert.ToDouble(topPValue);
        }

        if (config.TryGetValue("max_tokens", out object? maxTokensValue))
        {
            maxTokens = Convert.ToInt32(maxTokensValue);
        }

        return (model, temperature, topP, maxTokens);
    }

    /// <summary>
    ///     Gets model configuration for a specific model ID
    /// </summary>
    private ModelConfig? GetModelConfiguration(string modelId)
    {
        if (_config.ModelConfigurations == null)
            return null;

        return _config.ModelConfigurations.Values
            .FirstOrDefault(m => string.Equals(m.ModelId, modelId, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
///     Exception thrown by BedrockService operations
/// </summary>
public class BedrockServiceException : Exception
{
    public BedrockServiceException(string message) : base(message)
    {
    }

    public BedrockServiceException(string message, Exception innerException) : base(message,
        innerException)
    {
    }
}