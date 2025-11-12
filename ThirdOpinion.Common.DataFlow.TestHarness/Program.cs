using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ThirdOpinion.Common.DataFlow.DependencyInjection;
using ThirdOpinion.DataFlow.TestHarness.Persistence;
using ThirdOpinion.DataFlow.TestHarness.Pipelines;

using var host = await BuildHostAsync(args);

await EnsureDatabaseMigratedAsync(host.Services);
await RunSamplePipelineAsync(host.Services, CancellationToken.None);

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
