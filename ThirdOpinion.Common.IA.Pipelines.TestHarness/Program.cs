using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ThirdOpinion.Common.IA.Pipelines.DependencyInjection;
using ThirdOpinion.Common.IA.Pipelines.TestHarness.Persistence;
using ThirdOpinion.Common.IA.Pipelines.TestHarness.Pipelines;

using var host = await BuildHostAsync(args);

await EnsureDatabaseMigratedAsync(host.Services);
await RunSamplePipelineAsync(host.Services, CancellationToken.None);
await RunLargeRetryScenarioAsync(host.Services, CancellationToken.None);

static async Task<IHost> BuildHostAsync(string[] args)
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();

    builder.Configuration.SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
        .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);

    var connectionString = builder.Configuration.GetConnectionString("DataFlow");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("Connection string 'DataFlow' is required.");
    }

    builder.Services.AddDbContext<DataFlowTestDbContext>(options =>
    {
        options.UseNpgsql(connectionString, npgsql =>
        {
            npgsql.MigrationsAssembly(typeof(DataFlowTestDbContext).Assembly.FullName);
        });
    });

    builder.Services.AddThirdOpinionDataFlow()
        .AddEntityFrameworkStorage()
        .UseDbContext<DataFlowTestDbContext>()
        .ConfigureContextPool(options => options.MaxConcurrentContexts = 8)
        .WithEntityFrameworkServices();

    builder.Services.AddTransient<SamplePipelineRunner>();
    builder.Services.AddTransient<LargeRetryPipelineRunner>();

    return await Task.FromResult(builder.Build());
}

static async Task EnsureDatabaseMigratedAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<DataFlowTestDbContext>();
    await dbContext.Database.MigrateAsync();
}

static async Task RunSamplePipelineAsync(IServiceProvider services, CancellationToken cancellationToken)
{
    using var scope = services.CreateScope();
    var runner = scope.ServiceProvider.GetRequiredService<SamplePipelineRunner>();
    await runner.ExecuteAsync(cancellationToken);
}

static async Task RunLargeRetryScenarioAsync(IServiceProvider services, CancellationToken cancellationToken)
{
    using var scope = services.CreateScope();
    var runner = scope.ServiceProvider.GetRequiredService<LargeRetryPipelineRunner>();
    await runner.ExecuteAsync(cancellationToken);
}
