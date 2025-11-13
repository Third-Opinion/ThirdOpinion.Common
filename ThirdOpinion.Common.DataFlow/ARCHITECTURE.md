# ThirdOpinion.DataFlow Architecture

## Design Principles

### 1. Zero External Project Dependencies
The library depends only on NuGet packages:
- `System.Threading.Tasks.Dataflow` - TPL Dataflow blocks
- `Polly.Core` - Resilience policies
- `Microsoft.Extensions.Logging.Abstractions` - Logging interface

No references to any external projects, making it truly reusable.

### 2. Fully Generic
All types use generics (`PipelineResult<T>`, `DataFlowPipeline<T>`, etc.). No concrete DTOs or domain models are required.

### 3. Optional Services
Progress tracking and artifact storage are completely optional. The library works without them, making it suitable for simple scenarios.

### 4. Explicit Error Handling
Uses `PipelineResult<T>` pattern instead of throwing exceptions in the pipeline, allowing errors to flow through the pipeline without breaking the dataflow.

### 5. Fluent API
Clean, readable pipeline definitions that make the data flow obvious:

```csharp
DataFlowPipeline
    .Create<T>(context, id => id)
    .FromAsyncSource(source)
    .Transform(...)
    .WithArtifact(...)
    .Batch(100)
    .ExecuteAsync(...);
```

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                    DataFlowPipeline<T>                      │
│  Entry point for building pipelines                         │
└─────────────────┬───────────────────────────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────────────────────────┐
│               PipelineStepBuilder<TIn, TOut>                │
│  Chainable builder for adding transformation steps          │
└─────────────────┬───────────────────────────────────────────┘
                  │
                  ├─► Transform<TNext>() ──┐
                  ├─► TransformMany<TNext>()│
                  ├─► WithArtifact()        │
                  ├─► Batch()               │
                  └─► ExecuteAsync()        │
                                           │
                  ┌────────────────────────┘
                  │
┌─────────────────▼───────────────────────────────────────────┐
│                    Block Factories                          │
├─────────────────────────────────────────────────────────────┤
│  TrackedBlockFactory                                        │
│    - CreateInitialTrackedBlock<TInput, TOutput>            │
│    - CreateDownstreamTrackedBlock<TInput, TOutput>         │
│    - CreateDownstreamTrackedTransformMany<TIn, TOut>       │
│                                                             │
│  ArtifactBlockFactory                                       │
│    - CreateDownstreamBlockWithArtifacts<TIn, TOut>         │
│    - CreateInitialBlockWithArtifacts<TIn, TOut>            │
│                                                             │
│  DataFlowBlockFactory                                       │
│    - CreateAsyncEnumerableSource<T>                        │
│    - CreateBatchBlock<T>                                   │
│    - CreateBroadcastBlock<T>                               │
└─────────────────┬───────────────────────────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────────────────────────┐
│                   TPL Dataflow Blocks                       │
│  BufferBlock, TransformBlock, TransformManyBlock,           │
│  ActionBlock, BatchBlock, BroadcastBlock                    │
└─────────────────────────────────────────────────────────────┘
```

## Component Hierarchy

### Core
- `IPipelineContext` / `PipelineContext` - Execution context
- `DataFlowPipeline<T>` - Pipeline entry point
- `PipelineStepBuilder<TIn, TOut>` - Step builder
- `PipelineBatchBuilder<T>` - Batch operation builder
- `PipelineContextBuilder` - Context builder
- `PipelineStepOptions` - Step configuration
- `ArtifactOptions<T>` - Artifact configuration

### Results
- `PipelineResult<T>` - Error-aware result wrapper

### Blocks
- `TrackedBlockFactory` - Blocks with progress tracking
- `ArtifactBlockFactory` - Blocks with artifact capture
- `DataFlowBlockFactory` - Common block patterns

### Progress
- `IPipelineProgressTracker` - In-memory tracking interface
- `IPipelineProgressService` - Persistence interface
- `IResourceRunCache` - Resource ID caching
- Models: `ResourceProgressState`, `StepMetrics`, `PipelineSnapshot`, `PipelineRun`

### Artifacts
- `IArtifactStorageService` - Storage interface
- `IArtifactBatcher` - Batching interface
- Models: `ArtifactSaveRequest`, `ArtifactSaveResult`

### Models
- Enums: `PipelineResourceStatus`, `PipelineStepStatus`, `ArtifactStorageType`, `PipelineRunType`, `PipelineRunStatus`
- Configuration: `PipelineRunConfiguration`, update models

## Pipeline Execution Flow

```
1. Create Context
   └─► PipelineContextBuilder
       ├─► Add optional services
       └─► Build() → IPipelineContext

2. Create Pipeline
   └─► DataFlowPipeline.Create<T>(context, idSelector)
       └─► FromAsyncSource() / FromSource() / FromEnumerable()

3. Add Steps
   └─► Transform() / TransformMany()
       ├─► Creates TrackedBlock (wraps with progress tracking)
       ├─► Links to previous block
       └─► Returns PipelineStepBuilder<TIn, TNext>

4. Optional: Add Artifacts
   └─► WithArtifact()
       └─► Marks step for artifact capture

5. Optional: Batch Processing
   └─► Batch(size)
       └─► Returns PipelineBatchBuilder<T>

6. Execute
   └─► ExecuteAsync(action, stepName)
       ├─► Creates ActionBlock
       ├─► Awaits completion
       ├─► Finalizes ProgressTracker
       └─► Finalizes ArtifactBatcher
```

## PipelineResult<T> Pattern

### Why Not Exceptions?

Throwing exceptions in dataflow blocks can break the pipeline and make error handling complex. `PipelineResult<T>` allows errors to flow through the pipeline naturally.

### Pattern Details

```csharp
// Success
var result = PipelineResult<Data>.Success(data, resourceId, durationMs);

// Failure
var result = PipelineResult<Data>.Failure(resourceId, errorMessage, errorStep);

// Propagation
if (!result.IsSuccess)
{
    return PipelineResult<NewType>.Failure(
        result.ResourceId,
        result.ErrorMessage,
        result.ErrorStep);
}
```

### Benefits

- Errors don't break the pipeline
- Failed resources are tracked and logged
- Successful resources continue processing
- Easy to filter/handle errors at any stage

## Progress Tracking Internals

### In-Memory Tracking

`IPipelineProgressTracker` maintains state in memory:

```csharp
Dictionary<string, ResourceProgressState> _resourceStates;
```

Each resource tracks:
- Overall status (Pending, Processing, Completed, Failed)
- Per-step metrics (status, start/end time, duration, errors)
- Error details (message, step where error occurred)

### Batch Persistence

Progress is periodically flushed to storage via `IPipelineProgressService`:

```csharp
await progressService.CreateResourceRunsBatchAsync(runId, updates, ct);
await progressService.UpdateStepProgressBatchAsync(runId, stepUpdates, ct);
await progressService.CompleteResourceRunsBatchAsync(runId, completions, ct);
```

### Resource Path

Supports nested resources via string arrays:

```csharp
// Top-level resource
RecordStepStart(["patient-123"], "Extract");

// Child resource (e.g., from TransformMany)
RecordStepStart(["patient-123", "observation-456"], "Process");
```

## Artifact Storage Internals

### Queuing Pattern

Artifacts are queued asynchronously to avoid blocking the pipeline:

```csharp
1. Pipeline produces result
2. BroadcastBlock splits flow:
   ├─► Downstream processing (main flow)
   └─► Artifact saving (fire-and-forget)
3. ArtifactBatcher queues request
4. Batches are flushed periodically or at finalization
```

### Batching Benefits

- Reduces I/O operations
- Improves throughput
- Allows transactional saves
- Non-blocking pipeline execution

### Storage Types

```csharp
public enum ArtifactStorageType
{
    S3,           // AWS S3 or compatible
    Database,     // SQL/NoSQL database
    FileSystem,   // Local or network filesystem
    Memory        // In-memory (testing)
}
```

## Performance Considerations

### Parallelism

Control via `PipelineStepOptions`:

```csharp
new PipelineStepOptions
{
    MaxDegreeOfParallelism = 4,      // Parallel executions
    BoundedCapacity = 1000,          // Buffer size
    EnableProgressTracking = true
}
```

### Bounded Capacity

Prevents memory exhaustion by limiting buffered items:

```csharp
BoundedCapacity = 1000  // Max 1000 items in buffer
```

When buffer is full, upstream blocks wait.

### Batching

Use batching for I/O-heavy operations:

```csharp
.Batch(100)  // Process 100 items at once
.ExecuteAsync(async batch => await SaveBatchAsync(batch), "Save")
```

Benefits:
- Reduces database round-trips
- Enables bulk operations
- Improves throughput

### Memory Management

- `PipelineResult<T>` is a struct (stack-allocated when possible)
- Blocks release items after processing
- Completion propagation ensures timely cleanup
- CancellationToken allows graceful shutdown

## Extension Points

### Custom Progress Tracker

Implement `IPipelineProgressTracker`:

```csharp
public class MyProgressTracker : IPipelineProgressTracker
{
    public void Initialize(Guid runId, CancellationToken ct) { }
    public void RecordResourceStart(string resourceId, string resourceType) { }
    // ... implement other methods
}
```

### Custom Artifact Storage

Implement `IArtifactStorageService`:

```csharp
public class MyArtifactStorage : IArtifactStorageService
{
    public async Task<List<ArtifactSaveResult>> SaveBatchAsync(
        List<ArtifactSaveRequest> requests, 
        CancellationToken ct)
    {
        // Custom storage logic
    }
}
```

### Custom Blocks

Use `TrackedBlockFactory` to create custom tracked blocks:

```csharp
var customBlock = TrackedBlockFactory.CreateDownstreamTrackedBlock(
    myTransformAsync,
    "MyCustomStep",
    context,
    options);
```

## Integration with Existing Systems

### Adapting Existing Services

Create adapter classes to wrap existing services:

```csharp
public class MyProgressAdapter : IPipelineProgressService
{
    private readonly IMyExistingService _existingService;
    
    public async Task<PipelineRun> CreateRunAsync(CreatePipelineRunRequest request, CancellationToken ct)
    {
        // Adapt to existing service
        return await _existingService.CreateRun(
            request.Category, 
            request.RunType, 
            request.ParentRunId, 
            request.Config, 
            ct);
    }
}
```

### Service Injection

Use with dependency injection:

```csharp
services.AddScoped<IPipelineProgressService, MyProgressAdapter>();
services.AddScoped<IArtifactStorageService, MyArtifactAdapter>();
```

## Testing

### In-Memory Implementations

The library provides in-memory implementations for testing:

```csharp
var tracker = new InMemoryProgressTracker();
var storage = new InMemoryArtifactStorageService();
var batcher = new InMemoryArtifactBatcher(storage);
var cache = new InMemoryResourceRunCache();
```

### Unit Testing Pipelines

Test individual transformations:

```csharp
var result = await PipelineResult<Input>
    .Success(input, "test-id")
    .MapAsync(async data => await MyTransform(data));

Assert.True(result.IsSuccess);
```

### Integration Testing

Use in-memory services for full pipeline tests:

```csharp
var context = PipelineContextBuilder
    .CreateNew(Guid.NewGuid(), "Test")
    .WithProgressTracker(new InMemoryProgressTracker())
    .Build();

await DataFlowPipeline
    .Create<Data>(context, d => d.Id)
    .FromEnumerable(testData)
    .Transform(async d => await Process(d), "Process")
    .ExecuteAsync(async d => results.Add(d), "Collect");

Assert.Equal(expectedCount, results.Count);
```

## Best Practices

1. **Always provide a resource ID selector** - enables tracking and error correlation
2. **Use PipelineResult<T> consistently** - don't mix with exceptions in transforms
3. **Configure appropriate parallelism** - balance throughput and resource usage
4. **Use bounded capacity for large datasets** - prevents memory exhaustion
5. **Batch I/O operations** - reduces overhead and improves throughput
6. **Implement cancellation** - allows graceful shutdown
7. **Log liberally** - pipeline execution can be complex to debug
8. **Test with in-memory services** - fast, reliable tests
9. **Monitor progress** - use progress tracking to identify bottlenecks
10. **Capture artifacts strategically** - balance observability and storage costs

