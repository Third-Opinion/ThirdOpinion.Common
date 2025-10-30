using Amazon.Extensions.NETCore.Setup;
using Amazon.HealthLake;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ThirdOpinion.Common.Aws.HealthLake.Configuration;
using ThirdOpinion.Common.Sample.Services;
using ThirdOpinion.Common.Logging;
using ThirdOpinion.Common.Aws.HealthLake.Http;
using ThirdOpinion.Common.Aws.HealthLake;

namespace ThirdOpinion.Common.Sample;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Build configuration
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        // Build service provider
        var serviceProvider = BuildServiceProvider(configuration);

        // Get logger
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            logger.LogInformation("Starting ThirdOpinion.Common.Sample smoke test");

            // Get DocumentReference ID from command line args or configuration
            string? documentReferenceId = null;

            if (args.Length > 0)
            {
                documentReferenceId = args[0];
                logger.LogInformation("Using DocumentReference ID from command line: {DocumentReferenceId}", documentReferenceId);
            }
            else
            {
                documentReferenceId = configuration["Sample:DocumentReferenceId"];
                if (!string.IsNullOrEmpty(documentReferenceId))
                {
                    logger.LogInformation("Using DocumentReference ID from configuration: {DocumentReferenceId}", documentReferenceId);
                }
            }

            if (string.IsNullOrEmpty(documentReferenceId))
            {
                logger.LogError("No DocumentReference ID provided. Please provide it as a command line argument or set Sample:DocumentReferenceId in appsettings.json");
                Console.WriteLine("Usage: ThirdOpinion.Common.Sample <DocumentReference-ID>");
                Console.WriteLine("   or: Set Sample:DocumentReferenceId in appsettings.json");
                return 1;
            }

            // Get the HealthLakeReaderService
            var healthLakeReaderService = serviceProvider.GetRequiredService<HealthLakeReaderService>();

            // Test HealthLake connectivity first
            logger.LogInformation("Testing HealthLake connectivity...");

            // Retrieve the DocumentReference and extract content
            logger.LogInformation("Retrieving DocumentReference {DocumentReferenceId}...", documentReferenceId);

            var documentContent = await healthLakeReaderService.GetDocumentReferenceContentAsync(documentReferenceId);

            if (documentContent == null)
            {
                logger.LogError("Failed to retrieve or extract content from DocumentReference {DocumentReferenceId}", documentReferenceId);
                return 1;
            }

            // Determine output filename
            var outputDirectory = configuration["Sample:OutputDirectory"] ?? "./output";
            Directory.CreateDirectory(outputDirectory);

            var outputFilename = documentContent.Filename ?? $"document_{documentReferenceId}";
            if (!Path.HasExtension(outputFilename))
            {
                outputFilename += documentContent.GetFileExtension();
            }

            var outputPath = Path.Combine(outputDirectory, outputFilename);

            // Save the decoded document
            logger.LogInformation("Saving decoded document to {OutputPath} ({DataSize} bytes)", outputPath, documentContent.Data.Length);

            await File.WriteAllBytesAsync(outputPath, documentContent.Data);

            logger.LogInformation("Successfully completed smoke test!");
            Console.WriteLine($"✓ DocumentReference {documentReferenceId} retrieved successfully");
            Console.WriteLine($"✓ Document content decoded ({documentContent.Data.Length} bytes)");
            Console.WriteLine($"✓ Document saved to: {outputPath}");
            Console.WriteLine($"✓ MIME Type: {documentContent.MimeType ?? "unknown"}");

            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fatal error during smoke test execution");
            Console.WriteLine($"✗ Error: {ex.Message}");
            return 1;
        }
        finally
        {
            await serviceProvider.DisposeAsync();
        }
    }

    private static ServiceProvider BuildServiceProvider(IConfiguration configuration)
    {
        var services = new ServiceCollection();

        // Add configuration
        services.AddSingleton<IConfiguration>(configuration);

        // Add logging
        services.AddLogging(builder =>
        {
            builder.AddConfiguration(configuration.GetSection("Logging"));
            builder.AddConsole();
        });

        // Configure AWS options
        services.AddDefaultAWSOptions(configuration.GetAWSOptions());

        // Add AWS services
        services.AddAWSService<IAmazonHealthLake>();

        // Configure HealthLake
        services.Configure<HealthLakeConfig>(configuration.GetSection("HealthLake"));

        // Add HttpClient for HealthLake HTTP service
        services.AddHttpClient<IHealthLakeHttpService, HealthLakeHttpService>();

        // Add correlation ID provider
        services.AddScoped<ICorrelationIdProvider, CorrelationIdProvider>();

        // Add our custom services
        services.AddScoped<HealthLakeReaderService>();

        return services.BuildServiceProvider();
    }
}

/// <summary>
/// Simple correlation ID provider for tracking requests
/// </summary>
public class CorrelationIdProvider : ICorrelationIdProvider
{
    private string _correlationId;

    public CorrelationIdProvider()
    {
        _correlationId = Guid.NewGuid().ToString("N")[..8]; // Short correlation ID
    }

    public string GetCorrelationId() => _correlationId;

    public void SetCorrelationId(string correlationId)
    {
        _correlationId = correlationId ?? throw new ArgumentNullException(nameof(correlationId));
    }

    public IDisposable BeginScope(string? correlationId = null)
    {
        var previousId = _correlationId;
        if (!string.IsNullOrEmpty(correlationId))
        {
            _correlationId = correlationId;
        }

        return new CorrelationScope(() => _correlationId = previousId);
    }

    private class CorrelationScope : IDisposable
    {
        private readonly Action _restoreAction;

        public CorrelationScope(Action restoreAction)
        {
            _restoreAction = restoreAction;
        }

        public void Dispose()
        {
            _restoreAction();
        }
    }
}