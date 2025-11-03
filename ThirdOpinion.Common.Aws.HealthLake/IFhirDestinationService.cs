namespace ThirdOpinion.Common.Aws.HealthLake;

/// <summary>
///     Interface for writing FHIR resources to a destination service (e.g., AWS HealthLake)
/// </summary>
public interface IFhirDestinationService
{
    /// <summary>
    ///     Writes a FHIR resource to the destination service using PUT operation
    /// </summary>
    /// <param name="resourceType">The FHIR resource type (e.g., "Patient", "Observation")</param>
    /// <param name="resourceId">The unique identifier for the resource</param>
    /// <param name="resourceJson">The FHIR resource as JSON string</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task that completes when the resource is successfully written</returns>
    /// <exception cref="ArgumentException">Thrown when parameters are invalid</exception>
    /// <exception cref="FhirResourceException">Thrown when the operation fails</exception>
    Task PutResourceAsync(string resourceType,
        string resourceId,
        string resourceJson,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Writes a strongly-typed FHIR resource to the destination service
    /// </summary>
    /// <typeparam name="T">The type of the FHIR resource</typeparam>
    /// <param name="resourceType">The FHIR resource type</param>
    /// <param name="resourceId">The unique identifier for the resource</param>
    /// <param name="resource">The FHIR resource object</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task that completes when the resource is successfully written</returns>
    Task PutResourceAsync<T>(string resourceType,
        string resourceId,
        T resource,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    ///     Retrieves a strongly-typed FHIR resource from the destination service
    /// </summary>
    /// <typeparam name="T">The type of the FHIR resource</typeparam>
    /// <param name="resourceType">The FHIR resource type</param>
    /// <param name="resourceId">The unique identifier for the resource</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The deserialized FHIR resource object</returns>
    /// <exception cref="ArgumentException">Thrown when parameters are invalid</exception>
    /// <exception cref="HealthLakeException">Thrown when the operation fails</exception>
    Task<T> GetResourceAsync<T>(string resourceType,
        string resourceId,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    ///     Writes multiple FHIR resources in batch operation
    /// </summary>
    /// <param name="resources">Collection of resources to write, each containing resourceType, resourceId, and JSON content</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary with resource keys and success status</returns>
    Task<Dictionary<string, bool>> PutResourcesAsync(
        IEnumerable<(string ResourceType, string ResourceId, string ResourceJson)> resources,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks if a resource type is supported by the destination service
    /// </summary>
    /// <param name="resourceType">The FHIR resource type to check</param>
    /// <returns>True if the resource type is supported, false otherwise</returns>
    bool IsResourceTypeSupported(string resourceType);

    /// <summary>
    ///     Gets all supported FHIR resource types for this destination service
    /// </summary>
    /// <returns>Read-only collection of supported resource type names</returns>
    IReadOnlyList<string> GetSupportedResourceTypes();

    /// <summary>
    ///     Writes a FHIR resource to the destination service
    /// </summary>
    /// <param name="resourceJson">The FHIR resource as JSON string</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    Task<WriteResult> WriteResourceAsync(string resourceJson,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Result of a write operation
/// </summary>
public class WriteResult
{
    /// <summary>
    ///     Indicates whether the write operation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    ///     Error message if the operation failed, null if successful
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    ///     Indicates whether the operation can be retried
    /// </summary>
    public bool IsRetryable { get; set; }
}