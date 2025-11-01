using ThirdOpinion.Common.Fhir.Documents.Models;

namespace ThirdOpinion.Common.Fhir.Documents;

/// <summary>
///     Service interface for downloading DocumentReference resources from HealthLake
/// </summary>
public interface IDocumentDownloadService
{
    /// <summary>
    ///     Downloads all DocumentReference resources for a patient using Patient/$everything operation
    /// </summary>
    /// <param name="patientId">The patient ID to download documents for</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Download result containing success count and any errors</returns>
    Task<DocumentDownloadResult> DownloadPatientDocumentsAsync(
        string patientId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Downloads documents for multiple patients
    /// </summary>
    /// <param name="patientIds">Collection of patient IDs to process</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Overall download result for all patients</returns>
    Task<DocumentDownloadResult> DownloadMultiplePatientDocumentsAsync(
        IEnumerable<string> patientIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Downloads a specific DocumentReference resource
    /// </summary>
    /// <param name="documentReference">The DocumentReference to download</param>
    /// <param name="practiceInfo">Practice information for folder organization</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Download result for the specific document</returns>
    Task<DocumentDownloadResult> DownloadDocumentAsync(
        DocumentReferenceData documentReference,
        PracticeInfo practiceInfo,
        CancellationToken cancellationToken = default);
}