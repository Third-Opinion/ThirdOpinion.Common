namespace ThirdOpinion.Common.Aws.HealthLake;

/// <summary>
///     Service interface for retrieving FHIR resources from a source system
/// </summary>
public interface IFhirSourceService
{
    /// <summary>
    ///     Retrieves a FHIR resource as a JSON string
    /// </summary>
    /// <param name="resourceType">The FHIR resource type (e.g., Patient, Practitioner, Medication)</param>
    /// <param name="resourceId">The unique identifier of the resource</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>The FHIR resource as a JSON string, or null if not found</returns>
    Task<string?> GetResourceAsync(string resourceType,
        string resourceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Retrieves a FHIR resource and deserializes it to a strongly-typed object
    /// </summary>
    /// <typeparam name="T">The type to deserialize the resource to</typeparam>
    /// <param name="resourceType">The FHIR resource type</param>
    /// <param name="resourceId">The unique identifier of the resource</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>The deserialized FHIR resource, or null if not found</returns>
    Task<T?> GetResourceAsync<T>(string resourceType,
        string resourceId,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    ///     Retrieves multiple FHIR resources in batch
    /// </summary>
    /// <param name="resourceRequests">Collection of resource type and ID pairs</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Dictionary of resource IDs to JSON strings (null for not found resources)</returns>
    Task<Dictionary<string, string?>> GetResourcesAsync(
        IEnumerable<(string ResourceType, string ResourceId)> resourceRequests,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Validates if a resource type is supported
    /// </summary>
    /// <param name="resourceType">The FHIR resource type to validate</param>
    /// <returns>True if the resource type is supported</returns>
    bool IsResourceTypeSupported(string resourceType);

    /// <summary>
    ///     Gets the list of supported FHIR resource types
    /// </summary>
    /// <returns>Collection of supported resource type names</returns>
    IReadOnlyCollection<string> GetSupportedResourceTypes();
}