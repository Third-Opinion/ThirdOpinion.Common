using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ThirdOpinion.DataFlow.TestHarness.Persistence;

public class DataFlowTestDbContextFactory : IDesignTimeDbContextFactory<DataFlowTestDbContext>
{
    public DataFlowTestDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Development";

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DataFlow")
            ?? "Host=localhost;Port=5432;Database=thirdopinion_dataflow_harness;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<DataFlowTestDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsql =>
        {
            npgsql.MigrationsAssembly(typeof(DataFlowTestDbContext).Assembly.FullName);
        });

        return new DataFlowTestDbContext(optionsBuilder.Options);
    }
}
