# ThirdOpinion.DataFlow

A fully independent, reusable fluent pipeline library for building data processing pipelines in .NET. Zero dependencies on external projects - completely generic and self-contained.

## Features

- **Fluent API**: Clean, readable pipeline definitions
- **Generic & Reusable**: No concrete DTOs, works with any data type
- **Progress Tracking**: Optional in-memory and persistent progress monitoring
- **Artifact Storage**: Capture intermediate results with pluggable storage
- **Entity Framework Persistence**: Optional PostgreSQL-backed storage with fluent DI configuration
- **Explicit Error Handling**: `PipelineResult<T>` pattern for error propagation without exceptions
- **Parallel Processing**: Built on TPL Dataflow with configurable parallelism
- **Resilience**: Integrate with Polly for retry policies
- **Self-Contained**: Only external dependencies are NuGet packages

## Installation

```bash
dotnet add package ThirdOpinion.DataFlow
```

## Quick Start

```csharp
using ThirdOpinion.DataFlow.Core;
using Microsoft.Extensions.DependencyInjection;

// 1. Register services (in Startup/Program.cs)
services.AddThirdOpinionDataFlow()
    .AddEntityFrameworkStorage()
    .UseDbContext<MyDataFlowDbContext>()
    .ConfigureContextPool(options => options.MaxConcurrentContexts = 10)
    .WithEntityFrameworkServices();

// 2. Inject factory and create pipeline
public class MyService
{
    private readonly IPipelineContextFactory _contextFactory;
    
    public MyService(IPipelineContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }
    
    public async Task ProcessDataAsync()
    {
        // Create context with metadata (tracker created automatically)
        var context = _contextFactory
            .CreateBuilder<PatientData>()
            .WithCategory("DataProcessing")
            .WithName("PatientDataPipeline")
            .WithCancellationToken(cancellationToken)
            .Build();

        // Build and execute pipeline (context disposed automatically)
        await DataFlowPipeline<PatientData>
            .Create(context, p => p.Id)
            .FromAsyncSource(dataStream)
            .Transform(async data => await ProcessAsync(data), "Process")
                .WithArtifact(nameFactory: d => $"output_{d.Id}.json")
            .TransformMany(async data => await ExpandAsync(data), d => d.Id, "Expand")
            .Action(async result => await SaveAsync(result), "Save")
            .Complete();  // Finalizes and disposes context/tracker
    }
}
```

## Core Concepts

### Pipeline Context

The `IPipelineContext` contains run metadata and optional services. Use the factory to create contexts with automatic dependency injection:

```csharp
// Factory-based (recommended - handles DI and lifecycle)
var context = _contextFactory
    .CreateBuilder<MyResourceType>()
    .WithCategory("MyCategory")              // Required for tracking
    .WithName("MyPipelineName")              // Required for tracking
    .WithRunId(Guid.NewGuid())               // Optional (auto-generated if not provided)
    .WithCancellationToken(ct)
    .Build();

// Services are injected automatically:
// - IPipelineProgressTrackerFactory creates and initializes tracker
// - IArtifactBatcher for artifact capture
// - IResourceRunCache for caching
// - ILogger for logging
```

### Persistence Integration

To persist pipeline state with Entity Framework, the host application should:

1. Create a DbContext that implements `IDataFlowDbContext`, calls `modelBuilder.ApplyDataFlowModel("optional_schema")`, and exposes the required `DbSet<T>` properties.
2. Register that context in DI (e.g., `services.AddDbContext<MyDataFlowDbContext>(...)`).
3. Configure DataFlow using the fluent builder:

```csharp
services.AddThirdOpinionDataFlow()
    .AddEntityFrameworkStorage()
    .UseDbContext<MyDataFlowDbContext>()
    .ConfigureContextPool(options => options.MaxConcurrentContexts = 10)
    .WithEntityFrameworkServices();
```

This keeps the library portable while letting each host control migrations, schema naming, and connection management.

### PipelineResult<T>

Explicit error propagation without throwing exceptions:

```csharp
public class PipelineResult<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? ErrorMessage { get; }
    public string? ErrorStep { get; }
    public string ResourceId { get; }
}
```

### Progress Tracking

Progress trackers are created automatically via the factory pattern. Implement `IPipelineProgressTrackerFactory` for custom tracking:

```csharp
public interface IPipelineProgressTrackerFactory
{
    IPipelineProgressTracker Create(PipelineRunMetadata metadata, CancellationToken cancellationToken);
}

// Register your factory
services.AddThirdOpinionDataFlow()
    .WithProgressTrackerFactory<MyCustomFactory>();

// The factory receives metadata (RunId, Category, Name) and creates/initializes trackers
// Trackers are automatically disposed when the pipeline completes
```

The tracker interface:

```csharp
public interface IPipelineProgressTracker
{
    void Initialize(Guid runId, CancellationToken ct);  // Called by factory
    void RecordResourceStart(string resourceId, string resourceType);
    void RecordStepStart(string[] resourcePath, string stepName);
    void RecordStepComplete(string[] resourcePath, string stepName, int durationMs);
    void RecordStepFailed(string[] resourcePath, string stepName, int durationMs, string? errorMessage);
    void RecordResourceComplete(string resourceId, PipelineResourceStatus finalStatus, ...);
    Task FinalizeAsync();  // Called automatically before disposal
    PipelineSnapshot GetPipelineSnapshot();
}
```

### Artifact Storage

Implement `IArtifactStorageService` for custom storage:

```csharp
public interface IArtifactStorageService
{
    Task<List<ArtifactSaveResult>> SaveBatchAsync(
        List<ArtifactSaveRequest> requests, 
        CancellationToken ct);
}
```

## Pipeline Operations

### Transform

Transform one item to another:

```csharp
.Transform(async data => await ProcessAsync(data), "ProcessStep")
```

### TransformMany

Expand one item into multiple:

```csharp
.TransformMany(
    async data => await ExpandAsync(data), 
    item => item.ChildId,
    "ExpandStep")
```

### WithArtifact

Capture intermediate results:

```csharp
.Transform(async data => await ProcessAsync(data), "Process")
    .WithArtifact(
        artifactNameFactory: d => $"result_{d.Id}.json",
        storageType: ArtifactStorageType.S3)
```

### Batch

Process items in batches:

```csharp
.Batch(100)
.ExecuteAsync(async batch => await SaveBatchAsync(batch), "SaveBatch")
```

### ExecuteAsync

Terminal operation to execute the pipeline:

```csharp
.ExecuteAsync(async item => await SaveAsync(item), "Save")
```

## Advanced Examples

### With Entity Framework Storage

```csharp
services.AddDbContext<MyDataFlowDbContext>(options =>
    options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

services.AddThirdOpinionDataFlow()
    .AddEntityFrameworkStorage()
    .UseDbContext<MyDataFlowDbContext>()
    .ConfigureContextPool(options => options.MaxConcurrentContexts = 10)
    .WithEntityFrameworkServices();
```

### With In-Memory Services

```csharp
var services = new ServiceCollection()
    .AddThirdOpinionDataFlow()
    .UseInMemoryServices();
```

### With Custom Parallelism

```csharp
var options = new PipelineStepOptions
{
    MaxDegreeOfParallelism = 4,
    BoundedCapacity = 1000
};

.Transform(async data => await ProcessAsync(data), "Process", options)
```

### With Multiple Artifacts

```csharp
.Transform(async data => await ExtractFactsAsync(data), "ExtractFacts")
    .WithArtifact(nameFactory: d => $"facts_{d.Id}.json")
.Transform(async data => await EnrichAsync(data), "Enrich")
    .WithArtifact(nameFactory: d => $"enriched_{d.Id}.json")
```

## Architecture

See [ARCHITECTURE.md](ARCHITECTURE.md) for detailed design documentation.

## Examples

See the `Examples/` folder for complete working examples:

- `SimpleTransformPipeline.cs` - Basic transformation
- `PipelineWithArtifacts.cs` - Artifact capture
- `PipelineWithGrouping.cs` - Grouping/aggregation
- `PipelineWithRetry.cs` - Resilience with Polly
- `CustomProgressTracker.cs` - Custom progress tracker
- `CustomArtifactStorage.cs` - Custom artifact storage

## Dependencies

- `System.Threading.Tasks.Dataflow` - Core dataflow primitives
- `Polly.Core` - Resilience policies
- `Microsoft.Extensions.Logging.Abstractions` - Logging interface
- `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Relational`, `Npgsql.EntityFrameworkCore.PostgreSQL` - Optional EF Core persistence stack

## License

MIT License - see LICENSE file for details

