using ThirdOpinion.Common.Fhir.Documents.Models;

namespace ThirdOpinion.Common.Fhir.Documents;

/// <summary>
/// Service interface for retrieving Patient/$everything Bundle responses from HealthLake
/// </summary>
public interface IPatientEverythingService
{
    /// <summary>
    /// Retrieves all DocumentReference resources for a patient using Patient/$everything operation
    /// </summary>
    /// <param name="patientId">The patient ID to retrieve documents for</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Collection of all DocumentReference resources for the patient</returns>
    Task<IReadOnlyList<DocumentReferenceData>> GetPatientDocumentReferencesAsync(
        string patientId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single page of Patient/$everything Bundle response
    /// </summary>
    /// <param name="patientId">The patient ID to retrieve documents for</param>
    /// <param name="pageUrl">Optional URL for specific page (for pagination)</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Bundle response from HealthLake</returns>
    Task<BundleData> GetPatientEverythingBundleAsync(
        string patientId,
        string? pageUrl = null,
        CancellationToken cancellationToken = default);
}