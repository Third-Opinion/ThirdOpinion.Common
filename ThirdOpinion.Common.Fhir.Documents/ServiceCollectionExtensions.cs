using Microsoft.Extensions.DependencyInjection;

namespace ThirdOpinion.Common.Fhir.Documents;

/// <summary>
///     Extension methods for registering document-related services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Registers all document-related services for dependency injection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddDocumentServices(this IServiceCollection services)
    {
        // Document download services
        services.AddScoped<IPatientEverythingService, PatientEverythingService>();
        services.AddScoped<IBundleParserService, BundleParserService>();
        services.AddScoped<IFileOrganizationService, FileOrganizationService>();
        services.AddScoped<IBase64ContentExtractor, Base64ContentExtractor>();
        services.AddSingleton<INotFoundBinaryTracker, NotFoundBinaryTracker>();
        services.AddScoped<IBinaryDownloadService, BinaryDownloadService>();
        services.AddScoped<IMetadataExtractorService, MetadataExtractorService>();
        services.AddScoped<HealthLakeDocumentDownloadService>();

        // File naming and S3 storage services
        services.AddScoped<IFileNamingService, FileNamingService>();
        services.AddScoped<IS3StorageService, S3StorageService>();

        return services;
    }
}