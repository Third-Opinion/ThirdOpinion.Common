using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using ThirdOpinion.Common.Langfuse.Configuration;
using ThirdOpinion.Common.Logging;

// using ThirdOpinion.Common.Bedrock; - Removed AWS dependency

namespace ThirdOpinion.Common.Langfuse;

/// <summary>
///     HTTP client-based service for interacting with Langfuse API
/// </summary>
public class LangfuseService : ILangfuseService, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly LangfuseConfiguration _config;
    private readonly ICorrelationIdProvider _correlationIdProvider;
    private readonly Queue<object> _eventQueue = new();
    private readonly Timer _flushTimer;
    private readonly HttpClient _httpClient;
    private readonly ILogger<LangfuseService> _logger;
    private readonly object _queueLock = new();
    private readonly IAsyncPolicy _retryPolicy;

    /// <summary>
    ///     Initializes a new instance of the LangfuseService
    /// </summary>
    /// <param name="httpClientFactory">Factory for creating HTTP clients</param>
    /// <param name="config">Langfuse configuration options</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="correlationIdProvider">Correlation ID provider for request tracking</param>
    public LangfuseService(
        IHttpClientFactory httpClientFactory,
        IOptions<LangfuseConfiguration> config,
        ILogger<LangfuseService> logger,
        ICorrelationIdProvider correlationIdProvider)
    {
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _correlationIdProvider = correlationIdProvider ??
                                 throw new ArgumentNullException(nameof(correlationIdProvider));

        if (!_config.IsConfigured)
            _logger.LogWarning("Langfuse is not properly configured - API calls will be skipped");

        _httpClient = httpClientFactory?.CreateClient(nameof(LangfuseService))
                      ?? throw new ArgumentNullException(nameof(httpClientFactory));

        ConfigureHttpClient();
        _retryPolicy = CreateRetryPolicy();

        // Set up periodic flush timer
        _flushTimer = new Timer(
            async _ => await FlushAsync(),
            null,
            TimeSpan.FromSeconds(_config.FlushIntervalSeconds),
            TimeSpan.FromSeconds(_config.FlushIntervalSeconds));
    }

    public void Dispose()
    {
        _flushTimer?.Dispose();

        // Final flush before disposal
        try
        {
            FlushAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during final flush in Dispose");
        }

        _httpClient?.Dispose();
    }

    /// <inheritdoc />
    public async Task<LangfusePromptListResponse?> ListPromptsAsync(
        int? page = null,
        int? limit = null,
        string? name = null,
        string? label = null,
        string? tag = null,
        DateTime? fromCreatedAt = null,
        DateTime? toCreatedAt = null,
        int? version = null,
        CancellationToken cancellationToken = default)
    {
        if (!_config.IsConfigured)
        {
            _logger.LogDebug("Langfuse not configured - skipping prompt list retrieval");
            return null;
        }

        try
        {
            // Use the v2 prompts endpoint with query parameters
            string queryString = LangfuseQueryBuilder.BuildPromptsQuery(
                page, limit, name, label, tag, fromCreatedAt, toCreatedAt, version);

            var endpoint = $"v2/prompts{queryString}";

            _logger.LogDebug("Retrieving prompt list from Langfuse using endpoint: {Endpoint}",
                endpoint);

            HttpResponseMessage? response = await _retryPolicy.ExecuteAsync(async () =>
                await _httpClient.GetAsync(endpoint, cancellationToken));

            if (response.IsSuccessStatusCode)
            {
                string content = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogDebug("Raw response from Langfuse v2 prompts endpoint: {Content}",
                    content);

                var promptList
                    = JsonSerializer.Deserialize<LangfusePromptListResponse>(content, JsonOptions);

                _logger.LogDebug(
                    "Successfully retrieved {Count} prompts from Langfuse (page {Page}, total {TotalItems})",
                    promptList?.Data?.Count ?? 0,
                    promptList?.Meta?.Page ?? 1,
                    promptList?.Meta?.TotalItems ?? 0);

                return promptList;
            }

            string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Failed to retrieve prompt list from Langfuse: {StatusCode} {ReasonPhrase} Content: {Content}",
                response.StatusCode, response.ReasonPhrase, errorContent);

            // If the v2 endpoint fails, try fallback to v1
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning(
                    "Langfuse v2 prompts endpoint not found, falling back to v1 endpoint");
                return await ListPromptsV1FallbackAsync(cancellationToken);
            }

            return null;
        }
        catch (Exception ex)
        {
            string correlationId = _correlationIdProvider.GetCorrelationId();
            _logger.LogError(ex,
                "Error retrieving prompt list from Langfuse [CorrelationId: {CorrelationId}]",
                correlationId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<LangfusePromptResponse?> GetPromptAsync(string promptName,
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        if (!_config.IsConfigured)
        {
            _logger.LogDebug("Langfuse not configured - skipping prompt retrieval for {PromptName}",
                promptName);
            return null;
        }

        if (string.IsNullOrWhiteSpace(promptName))
            throw new ArgumentException("Prompt name cannot be null or empty", nameof(promptName));

        try
        {
            // Try v2 endpoint first (consistent with list endpoint)
            var endpoint = $"v2/prompts/{Uri.EscapeDataString(promptName)}";
            if (!string.IsNullOrWhiteSpace(version))
                endpoint += $"?version={Uri.EscapeDataString(version)}";

            _logger.LogInformation(
                "Retrieving prompt from Langfuse: {PromptName} version {Version} using endpoint: {Endpoint}",
                promptName, version ?? "latest", endpoint);

            HttpResponseMessage? response = await _retryPolicy.ExecuteAsync(async () =>
                await _httpClient.GetAsync(endpoint, cancellationToken));

            if (response.IsSuccessStatusCode)
            {
                string content = await response.Content.ReadAsStringAsync(cancellationToken);
                var prompt
                    = JsonSerializer.Deserialize<LangfusePromptResponse>(content, JsonOptions);

                _logger.LogInformation(
                    "Successfully retrieved prompt: {PromptName} version {Version}",
                    promptName, prompt?.Version);

                return prompt;
            }

            // If v2 fails, try v1 fallback
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogInformation(
                    "V2 endpoint not found, trying v1 fallback for prompt: {PromptName}",
                    promptName);
                return await GetPromptV1FallbackAsync(promptName, version, cancellationToken);
            }

            string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Failed to retrieve prompt from Langfuse v2: {StatusCode} {ReasonPhrase} Content: {Content} Endpoint: {Endpoint}",
                response.StatusCode, response.ReasonPhrase, errorContent, endpoint);

            // Try v1 fallback on any non-success response
            _logger.LogInformation("Trying v1 fallback due to v2 error for prompt: {PromptName}",
                promptName);
            return await GetPromptV1FallbackAsync(promptName, version, cancellationToken);
        }
        catch (Exception ex)
        {
            string correlationId = _correlationIdProvider.GetCorrelationId();
            _logger.LogError(ex,
                "Error retrieving prompt from Langfuse: {PromptName} [CorrelationId: {CorrelationId}]",
                promptName, correlationId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<LangfuseTraceResponse?> CreateTraceAsync(string traceId,
        string name,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        if (!_config.IsConfigured)
        {
            _logger.LogDebug("Langfuse not configured - skipping trace creation for {TraceId}",
                traceId);
            return null;
        }

        if (string.IsNullOrWhiteSpace(traceId))
            throw new ArgumentException("Trace ID cannot be null or empty", nameof(traceId));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Trace name cannot be null or empty", nameof(name));

        var request = new LangfuseTraceRequest
        {
            Id = traceId,
            Name = name,
            Metadata = metadata,
            Timestamp = DateTime.UtcNow
        };

        if (_config.EnableTelemetry) QueueEvent(request);

        try
        {
            string jsonContent = JsonSerializer.Serialize(request, JsonOptions);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _logger.LogDebug("Creating trace in Langfuse: {TraceId} - {TraceName}", traceId, name);

            HttpResponseMessage? response = await _retryPolicy.ExecuteAsync(async () =>
                await _httpClient.PostAsync("traces", httpContent, cancellationToken));

            if (response.IsSuccessStatusCode)
            {
                string content = await response.Content.ReadAsStringAsync(cancellationToken);
                var trace = JsonSerializer.Deserialize<LangfuseTraceResponse>(content, JsonOptions);

                _logger.LogDebug("Successfully created trace: {TraceId}", traceId);
                return trace;
            }

            _logger.LogError("Failed to create trace in Langfuse: {StatusCode} {ReasonPhrase}",
                response.StatusCode, response.ReasonPhrase);
            return null;
        }
        catch (Exception ex)
        {
            string correlationId = _correlationIdProvider.GetCorrelationId();
            _logger.LogError(ex,
                "Error creating trace in Langfuse: {TraceId} [CorrelationId: {CorrelationId}]",
                traceId, correlationId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<LangfuseObservationResponse?> CreateObservationAsync(
        string traceId,
        string observationId,
        string type,
        string name,
        object? input = null,
        object? output = null,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        if (!_config.IsConfigured)
        {
            _logger.LogDebug(
                "Langfuse not configured - skipping observation creation for {ObservationId}",
                observationId);
            return null;
        }

        if (string.IsNullOrWhiteSpace(traceId))
            throw new ArgumentException("Trace ID cannot be null or empty", nameof(traceId));

        if (string.IsNullOrWhiteSpace(observationId))
            throw new ArgumentException("Observation ID cannot be null or empty",
                nameof(observationId));

        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Observation type cannot be null or empty", nameof(type));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Observation name cannot be null or empty", nameof(name));

        var request = new LangfuseObservationRequest
        {
            Id = observationId,
            TraceId = traceId,
            Type = type,
            Name = name,
            Input = input,
            Output = output,
            Metadata = metadata,
            StartTime = DateTime.UtcNow
        };

        if (_config.EnableTelemetry) QueueEvent(request);

        try
        {
            string jsonContent = JsonSerializer.Serialize(request, JsonOptions);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _logger.LogDebug("Creating observation in Langfuse: {ObservationId} in trace {TraceId}",
                observationId, traceId);

            HttpResponseMessage? response = await _retryPolicy.ExecuteAsync(async () =>
                await _httpClient.PostAsync("observations", httpContent, cancellationToken));

            if (response.IsSuccessStatusCode)
            {
                string content = await response.Content.ReadAsStringAsync(cancellationToken);
                var observation
                    = JsonSerializer.Deserialize<LangfuseObservationResponse>(content, JsonOptions);

                _logger.LogDebug("Successfully created observation: {ObservationId}",
                    observationId);
                return observation;
            }

            _logger.LogError(
                "Failed to create observation in Langfuse: {StatusCode} {ReasonPhrase}",
                response.StatusCode, response.ReasonPhrase);
            return null;
        }
        catch (Exception ex)
        {
            string correlationId = _correlationIdProvider.GetCorrelationId();
            _logger.LogError(ex,
                "Error creating observation in Langfuse: {ObservationId} [CorrelationId: {CorrelationId}]",
                observationId, correlationId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<LangfuseObservationResponse?> UpdateObservationAsync(
        string observationId,
        object? output = null,
        LangfuseUsage? usage = null,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        if (!_config.IsConfigured)
        {
            _logger.LogDebug(
                "Langfuse not configured - skipping observation update for {ObservationId}",
                observationId);
            return null;
        }

        if (string.IsNullOrWhiteSpace(observationId))
            throw new ArgumentException("Observation ID cannot be null or empty",
                nameof(observationId));

        var request = new LangfuseObservationUpdateRequest
        {
            EndTime = DateTime.UtcNow,
            Output = output,
            Usage = usage,
            Metadata = metadata
        };

        try
        {
            string jsonContent = JsonSerializer.Serialize(request, JsonOptions);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _logger.LogDebug("Updating observation in Langfuse: {ObservationId}", observationId);

            HttpResponseMessage? response = await _retryPolicy.ExecuteAsync(async () =>
                await _httpClient.PatchAsync($"observations/{Uri.EscapeDataString(observationId)}",
                    httpContent, cancellationToken));

            if (response.IsSuccessStatusCode)
            {
                string content = await response.Content.ReadAsStringAsync(cancellationToken);
                var observation
                    = JsonSerializer.Deserialize<LangfuseObservationResponse>(content, JsonOptions);

                _logger.LogDebug("Successfully updated observation: {ObservationId}",
                    observationId);
                return observation;
            }

            _logger.LogError(
                "Failed to update observation in Langfuse: {StatusCode} {ReasonPhrase}",
                response.StatusCode, response.ReasonPhrase);
            return null;
        }
        catch (Exception ex)
        {
            string correlationId = _correlationIdProvider.GetCorrelationId();
            _logger.LogError(ex,
                "Error updating observation in Langfuse: {ObservationId} [CorrelationId: {CorrelationId}]",
                observationId, correlationId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        if (!_config.IsConfigured || !_config.EnableTelemetry) return;

        List<object> eventsToFlush;
        lock (_queueLock)
        {
            if (_eventQueue.Count == 0) return;

            eventsToFlush = new List<object>(_eventQueue);
            _eventQueue.Clear();
        }

        _logger.LogDebug("Flushing {EventCount} events to Langfuse", eventsToFlush.Count);

        try
        {
            var batch = new { batch = eventsToFlush };
            string jsonContent = JsonSerializer.Serialize(batch, JsonOptions);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            HttpResponseMessage? response = await _retryPolicy.ExecuteAsync(async () =>
                await _httpClient.PostAsync("ingestion", httpContent, cancellationToken));

            if (response.IsSuccessStatusCode)
                _logger.LogDebug("Successfully flushed {EventCount} events to Langfuse",
                    eventsToFlush.Count);
            else
                _logger.LogError("Failed to flush events to Langfuse: {StatusCode} {ReasonPhrase}",
                    response.StatusCode, response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error flushing events to Langfuse");
        }
    }

    /// <inheritdoc />
    public async Task<LangfuseObservationListResponse?> ListObservationsAsync(
        int? page = null,
        int? limit = null,
        string? name = null,
        string? type = null,
        string? traceId = null,
        DateTime? fromStartTime = null,
        DateTime? toStartTime = null,
        CancellationToken cancellationToken = default)
    {
        if (!_config.IsConfigured)
        {
            _logger.LogDebug("Langfuse not configured - skipping observation list retrieval");
            return null;
        }

        try
        {
            string queryString = LangfuseQueryBuilder.BuildObservationsQuery(
                page, limit, name, type, traceId, fromStartTime, toStartTime);

            var endpoint = $"observations{queryString}";

            _logger.LogDebug("Retrieving observation list from Langfuse using endpoint: {Endpoint}",
                endpoint);

            HttpResponseMessage? response = await _retryPolicy.ExecuteAsync(async () =>
                await _httpClient.GetAsync(endpoint, cancellationToken));

            if (response.IsSuccessStatusCode)
            {
                string content = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogDebug("Raw response from Langfuse observations endpoint: {Content}",
                    content);

                var observationList
                    = JsonSerializer.Deserialize<LangfuseObservationListResponse>(content,
                        JsonOptions);

                _logger.LogDebug(
                    "Successfully retrieved {Count} observations from Langfuse (page {Page}, total {TotalItems})",
                    observationList?.Data?.Count ?? 0,
                    observationList?.Meta?.Page ?? 1,
                    observationList?.Meta?.TotalItems ?? 0);

                return observationList;
            }

            string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Failed to retrieve observation list from Langfuse: {StatusCode} {ReasonPhrase} Content: {Content}",
                response.StatusCode, response.ReasonPhrase, errorContent);

            return null;
        }
        catch (Exception ex)
        {
            string correlationId = _correlationIdProvider.GetCorrelationId();
            _logger.LogError(ex,
                "Error retrieving observation list from Langfuse [CorrelationId: {CorrelationId}]",
                correlationId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<LangfuseTraceDetail>> GetTracesByPromptAsync(
        string promptName,
        int? page = null,
        int? limit = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(promptName))
            throw new ArgumentException("Prompt name cannot be null or empty", nameof(promptName));

        if (!_config.IsConfigured)
        {
            _logger.LogDebug(
                "Langfuse not configured - skipping trace retrieval for prompt {PromptName}",
                promptName);
            return new List<LangfuseTraceDetail>();
        }

        try
        {
            _logger.LogDebug("Retrieving traces for prompt: {PromptName}", promptName);

            // Get observations filtered by prompt name through metadata
            LangfuseObservationListResponse? observations = await ListObservationsAsync(
                page,
                limit ?? 200, // Get more observations to find traces
                promptName, // Try name filter first
                fromStartTime: fromDate,
                toStartTime: toDate,
                cancellationToken: cancellationToken);

            if (observations?.Data == null || !observations.Data.Any())
            {
                _logger.LogInformation("No observations found for prompt: {PromptName}",
                    promptName);
                return new List<LangfuseTraceDetail>();
            }

            // Group observations by traceId and build trace details
            List<IGrouping<string, LangfuseObservationResponse>> traceGroups = observations.Data
                .Where(obs => !string.IsNullOrEmpty(obs.TraceId))
                .GroupBy(obs => obs.TraceId)
                .Take(limit ?? 50) // Limit the number of traces
                .ToList();

            var traceDetails = new List<LangfuseTraceDetail>();

            foreach (IGrouping<string, LangfuseObservationResponse> traceGroup in traceGroups)
            {
                List<LangfuseObservationResponse> traceObservations = traceGroup.ToList();
                LangfuseObservationResponse firstObservation
                    = traceObservations.OrderBy(o => o.StartTime).First();
                LangfuseObservationResponse lastObservation = traceObservations
                    .OrderByDescending(o => o.EndTime ?? o.StartTime).First();

                // Calculate aggregated metrics
                int totalInputTokens = traceObservations.Sum(o => o.Usage?.Input ?? 0);
                int totalOutputTokens = traceObservations.Sum(o => o.Usage?.Output ?? 0);
                int totalTokens = traceObservations.Sum(o => o.Usage?.Total ?? 0);

                // Calculate duration
                TimeSpan duration = (lastObservation.EndTime ?? lastObservation.StartTime) -
                                    firstObservation.StartTime;

                // Determine status based on observations
                string status = traceObservations.Any(o => o.EndTime == null)
                    ? "In Progress"
                    : "Completed";

                // Extract model information from metadata
                string? model = null;
                foreach (LangfuseObservationResponse obs in traceObservations)
                    if (obs.Metadata?.TryGetValue("model", out object? modelValue) == true)
                    {
                        model = modelValue?.ToString();
                        break;
                    }

                var traceDetail = new LangfuseTraceDetail
                {
                    Id = traceGroup.Key,
                    Name = firstObservation.Name,
                    Timestamp = firstObservation.StartTime,
                    PromptName = promptName,
                    Model = model,
                    InputTokens = totalInputTokens > 0 ? totalInputTokens : null,
                    OutputTokens = totalOutputTokens > 0 ? totalOutputTokens : null,
                    TotalTokens = totalTokens > 0 ? totalTokens : null,
                    Duration = duration > TimeSpan.Zero ? duration : null,
                    Status = status,
                    Tags = firstObservation.Metadata?.ContainsKey("tags") == true
                        ? firstObservation.Metadata["tags"] as List<string>
                        : null,
                    Metadata = firstObservation.Metadata,
                    Observations = traceObservations
                };

                traceDetails.Add(traceDetail);
            }

            _logger.LogDebug("Successfully processed {TraceCount} traces for prompt: {PromptName}",
                traceDetails.Count, promptName);

            return traceDetails.OrderByDescending(t => t.Timestamp).ToList();
        }
        catch (Exception ex)
        {
            string correlationId = _correlationIdProvider.GetCorrelationId();
            _logger.LogError(ex,
                "Error retrieving traces for prompt {PromptName} [CorrelationId: {CorrelationId}]",
                promptName, correlationId);
            throw;
        }
    }

    // =================== BEDROCK TRACING METHODS ===================

    public async Task<LangfuseObservationResponse?> CreateGenerationAsync(
        LangfuseGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_config.IsConfigured)
        {
            _logger.LogDebug("Langfuse not configured - skipping generation creation");
            return null;
        }

        if (request == null) throw new ArgumentNullException(nameof(request));

        try
        {
            string jsonContent = JsonSerializer.Serialize(request, JsonOptions);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _logger.LogDebug("Creating generation in Langfuse: {GenerationId} for trace {TraceId}",
                request.Id, request.TraceId);

            HttpResponseMessage? response = await _retryPolicy.ExecuteAsync(async () =>
                await _httpClient.PostAsync("generations", httpContent, cancellationToken));

            if (response.IsSuccessStatusCode)
            {
                string content = await response.Content.ReadAsStringAsync(cancellationToken);
                var generation
                    = JsonSerializer.Deserialize<LangfuseObservationResponse>(content, JsonOptions);

                _logger.LogDebug("Successfully created generation: {GenerationId}", request.Id);
                return generation;
            }

            string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Failed to create generation in Langfuse: {StatusCode} {ReasonPhrase} - {Content}",
                response.StatusCode, response.ReasonPhrase, errorContent);
            return null;
        }
        catch (Exception ex)
        {
            string correlationId = _correlationIdProvider.GetCorrelationId();
            _logger.LogError(ex,
                "Error creating generation in Langfuse: {GenerationId} [CorrelationId: {CorrelationId}]",
                request.Id, correlationId);
            return null;
        }
    }

    public async Task<LangfuseIngestionResponse?> SendIngestionBatchAsync(
        LangfuseIngestionBatch batch,
        CancellationToken cancellationToken = default)
    {
        if (!_config.IsConfigured)
        {
            _logger.LogDebug("Langfuse not configured - skipping batch ingestion");
            return null;
        }

        if (batch?.Batch == null || !batch.Batch.Any())
            throw new ArgumentException("Batch cannot be null or empty", nameof(batch));

        try
        {
            string jsonContent = JsonSerializer.Serialize(batch, JsonOptions);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _logger.LogDebug("Sending ingestion batch to Langfuse with {EventCount} events",
                batch.Batch.Count);

            HttpResponseMessage? response = await _retryPolicy.ExecuteAsync(async () =>
                await _httpClient.PostAsync("ingestion", httpContent, cancellationToken));

            string content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode == HttpStatusCode.MultiStatus || response.IsSuccessStatusCode)
            {
                var ingestionResponse
                    = JsonSerializer.Deserialize<LangfuseIngestionResponse>(content, JsonOptions);

                _logger.LogDebug(
                    "Batch ingestion completed: {SuccessCount} successes, {ErrorCount} errors",
                    ingestionResponse?.Successes?.Count ?? 0,
                    ingestionResponse?.Errors?.Count ?? 0);

                if (ingestionResponse?.Errors?.Any() == true)
                    foreach (LangfuseIngestionError error in ingestionResponse.Errors)
                        _logger.LogWarning(
                            "Ingestion error for event {EventId}: {Status} - {Message}",
                            error.Id, error.Status, error.Message);

                return ingestionResponse;
            }

            _logger.LogError("Failed batch ingestion: {StatusCode} {ReasonPhrase} - {Content}",
                response.StatusCode, response.ReasonPhrase, content);
            return null;
        }
        catch (Exception ex)
        {
            string correlationId = _correlationIdProvider.GetCorrelationId();
            _logger.LogError(ex,
                "Error sending ingestion batch to Langfuse [CorrelationId: {CorrelationId}]",
                correlationId);
            return null;
        }
    }

    public async Task<LangfuseTraceResponse?> CreateBedrockTraceAsync(
        LangfuseBedrockTraceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_config.IsConfigured)
        {
            _logger.LogDebug("Langfuse not configured - skipping Bedrock trace creation");
            return null;
        }

        if (request == null) throw new ArgumentNullException(nameof(request));

        try
        {
            string jsonContent = JsonSerializer.Serialize(request, JsonOptions);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _logger.LogDebug("Creating Bedrock trace in Langfuse: {TraceId}", request.Id);

            HttpResponseMessage? response = await _retryPolicy.ExecuteAsync(async () =>
                await _httpClient.PostAsync("traces", httpContent, cancellationToken));

            if (response.IsSuccessStatusCode)
            {
                string content = await response.Content.ReadAsStringAsync(cancellationToken);
                var trace = JsonSerializer.Deserialize<LangfuseTraceResponse>(content, JsonOptions);

                _logger.LogDebug("Successfully created Bedrock trace: {TraceId}", request.Id);
                return trace;
            }

            string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Failed to create Bedrock trace in Langfuse: {StatusCode} {ReasonPhrase} - {Content}",
                response.StatusCode, response.ReasonPhrase, errorContent);
            return null;
        }
        catch (Exception ex)
        {
            string correlationId = _correlationIdProvider.GetCorrelationId();
            _logger.LogError(ex,
                "Error creating Bedrock trace in Langfuse: {TraceId} [CorrelationId: {CorrelationId}]",
                request.Id, correlationId);
            return null;
        }
    }

    // TODO: Implement provider-agnostic cost calculation
    /*
    public async Task<BedrockCostCalculation> CalculateBedrockCostAsync(
        string modelId,
        int inputTokens,
        int outputTokens)
    {
        // This method will be injected with IBedrockPricingService once we update DI
        // For now, create a temporary instance
        var pricingService = new BedrockPricingService();
        return await Task.FromResult(pricingService.CalculateCost(modelId, inputTokens, outputTokens));
    }
    */

    public async Task<LangfuseIngestionResponse?> CreateBedrockTraceAndGenerationAsync(
        LangfuseBedrockTraceRequest traceRequest,
        LangfuseGenerationRequest generationRequest,
        CancellationToken cancellationToken = default)
    {
        if (!_config.IsConfigured)
        {
            _logger.LogDebug(
                "Langfuse not configured - skipping Bedrock trace and generation creation");
            return null;
        }

        if (traceRequest == null) throw new ArgumentNullException(nameof(traceRequest));

        if (generationRequest == null) throw new ArgumentNullException(nameof(generationRequest));

        // Ensure the generation is linked to the trace
        generationRequest.TraceId = traceRequest.Id;

        // TODO: Reimplement without BedrockTracingExtensions
        // var batch = BedrockTracingExtensions.CreateIngestionBatch(traceRequest, generationRequest);
        // return await SendIngestionBatchAsync(batch, cancellationToken);
        return null;
    }

    public async Task<LangfuseObservationResponse?> UpdateGenerationAsync(
        string generationId,
        DateTime? endTime = null,
        object? output = null,
        LangfuseUsage? usage = null,
        Dictionary<string, object>? metadata = null,
        string? level = null,
        string? statusMessage = null,
        CancellationToken cancellationToken = default)
    {
        if (!_config.IsConfigured)
        {
            _logger.LogDebug(
                "Langfuse not configured - skipping generation update for {GenerationId}",
                generationId);
            return null;
        }

        if (string.IsNullOrWhiteSpace(generationId))
            throw new ArgumentException("Generation ID cannot be null or empty",
                nameof(generationId));

        var updateRequest = new Dictionary<string, object>();

        if (endTime.HasValue)
            updateRequest["endTime"] = endTime.Value.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

        if (output != null)
            updateRequest["output"] = output;

        if (usage != null)
            updateRequest["usage"] = usage;

        if (metadata != null)
            updateRequest["metadata"] = metadata;

        if (!string.IsNullOrWhiteSpace(level))
            updateRequest["level"] = level;

        if (!string.IsNullOrWhiteSpace(statusMessage))
            updateRequest["statusMessage"] = statusMessage;

        if (!updateRequest.Any())
        {
            _logger.LogDebug("No updates provided for generation {GenerationId}", generationId);
            return null;
        }

        try
        {
            string jsonContent = JsonSerializer.Serialize(updateRequest, JsonOptions);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _logger.LogDebug("Updating generation in Langfuse: {GenerationId}", generationId);

            HttpResponseMessage? response = await _retryPolicy.ExecuteAsync(async () =>
                await _httpClient.PatchAsync($"generations/{Uri.EscapeDataString(generationId)}",
                    httpContent, cancellationToken));

            if (response.IsSuccessStatusCode)
            {
                string content = await response.Content.ReadAsStringAsync(cancellationToken);
                var generation
                    = JsonSerializer.Deserialize<LangfuseObservationResponse>(content, JsonOptions);

                _logger.LogDebug("Successfully updated generation: {GenerationId}", generationId);
                return generation;
            }

            string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Failed to update generation in Langfuse: {StatusCode} {ReasonPhrase} - {Content}",
                response.StatusCode, response.ReasonPhrase, errorContent);
            return null;
        }
        catch (Exception ex)
        {
            string correlationId = _correlationIdProvider.GetCorrelationId();
            _logger.LogError(ex,
                "Error updating generation in Langfuse: {GenerationId} [CorrelationId: {CorrelationId}]",
                generationId, correlationId);
            return null;
        }
    }

    private void ConfigureHttpClient()
    {
        _httpClient.BaseAddress = new Uri(_config.BaseUrl.TrimEnd('/') + "/api/public/");
        _httpClient.Timeout = TimeSpan.FromSeconds(_config.RequestTimeoutSeconds);

        // Configure Basic Auth using public and secret keys
        string authValue
            = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{_config.PublicKey}:{_config.SecretKey}"));
        _httpClient.DefaultRequestHeaders.Authorization
            = new AuthenticationHeaderValue("Basic", authValue);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "FhirTools/1.0");
    }

    /// <summary>
    ///     Fallback method for v1 API compatibility
    /// </summary>
    private async Task<LangfusePromptListResponse?> ListPromptsV1FallbackAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var endpoint = "prompts";
            _logger.LogDebug("Using v1 fallback endpoint: {Endpoint}", endpoint);

            HttpResponseMessage? response = await _retryPolicy.ExecuteAsync(async () =>
                await _httpClient.GetAsync(endpoint, cancellationToken));

            if (response.IsSuccessStatusCode)
            {
                string content = await response.Content.ReadAsStringAsync(cancellationToken);
                var promptList
                    = JsonSerializer.Deserialize<LangfusePromptListResponse>(content, JsonOptions);

                _logger.LogDebug("Successfully retrieved {Count} prompts using v1 fallback",
                    promptList?.Data?.Count ?? 0);

                return promptList;
            }

            string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "V1 fallback also failed: {StatusCode} {ReasonPhrase} Content: {Content}",
                response.StatusCode, response.ReasonPhrase, errorContent);

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in v1 fallback for prompt list retrieval");
            return null;
        }
    }

    /// <summary>
    ///     Fallback method for v1 API compatibility for individual prompt retrieval
    /// </summary>
    private async Task<LangfusePromptResponse?> GetPromptV1FallbackAsync(string promptName,
        string? version,
        CancellationToken cancellationToken)
    {
        try
        {
            var endpoint = $"prompts/{Uri.EscapeDataString(promptName)}";
            if (!string.IsNullOrWhiteSpace(version))
                endpoint += $"?version={Uri.EscapeDataString(version)}";

            _logger.LogInformation(
                "Using v1 fallback endpoint for prompt: {PromptName} - {Endpoint}", promptName,
                endpoint);

            HttpResponseMessage? response = await _retryPolicy.ExecuteAsync(async () =>
                await _httpClient.GetAsync(endpoint, cancellationToken));

            if (response.IsSuccessStatusCode)
            {
                string content = await response.Content.ReadAsStringAsync(cancellationToken);
                var prompt
                    = JsonSerializer.Deserialize<LangfusePromptResponse>(content, JsonOptions);

                _logger.LogInformation(
                    "Successfully retrieved prompt using v1 fallback: {PromptName} version {Version}",
                    promptName, prompt?.Version);

                return prompt;
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning(
                    "Prompt not found in Langfuse using v1 fallback: {PromptName} version {Version} (404 Not Found)",
                    promptName, version ?? "latest");
                return null;
            }

            string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "V1 fallback also failed for prompt: {PromptName} - {StatusCode} {ReasonPhrase} Content: {Content}",
                promptName, response.StatusCode, response.ReasonPhrase, errorContent);

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in v1 fallback for prompt retrieval: {PromptName}",
                promptName);
            return null;
        }
    }

    private void QueueEvent(object eventData)
    {
        lock (_queueLock)
        {
            _eventQueue.Enqueue(eventData);

            // Auto-flush if batch size reached
            if (_eventQueue.Count >= _config.BatchSize)
                _ = Task.Run(async () => await FlushAsync());
        }
    }

    private IAsyncPolicy CreateRetryPolicy()
    {
        return Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .Or<TimeoutException>()
            .WaitAndRetryAsync(
                _config.MaxRetries,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timespan, retryCount, context) =>
                {
                    _logger.LogWarning(exception,
                        "Retry attempt {RetryCount} for Langfuse API call. Waiting {Delay}ms before next retry",
                        retryCount, timespan.TotalMilliseconds);
                });
    }
}