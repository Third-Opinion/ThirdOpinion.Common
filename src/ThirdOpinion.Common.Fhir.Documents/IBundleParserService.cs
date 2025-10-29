using ThirdOpinion.Common.Fhir.Documents.Models;

namespace ThirdOpinion.Common.Fhir.Documents;

/// <summary>
/// Service interface for parsing FHIR Bundle resources
/// </summary>
public interface IBundleParserService
{
    /// <summary>
    /// Parses a Bundle JSON string and extracts DocumentReference resources
    /// </summary>
    /// <param name="bundleJson">The Bundle JSON string from HealthLake</param>
    /// <returns>Parsed Bundle data with DocumentReference resources</returns>
    BundleData ParseBundle(string bundleJson);

    /// <summary>
    /// Validates that a Bundle is a valid searchset Bundle
    /// </summary>
    /// <param name="bundle">The Bundle to validate</param>
    /// <returns>True if valid searchset Bundle</returns>
    bool ValidateSearchsetBundle(BundleData bundle);

    /// <summary>
    /// Extracts practice information from DocumentReference extensions
    /// </summary>
    /// <param name="documentReference">The DocumentReference to extract from</param>
    /// <returns>Practice information if found</returns>
    PracticeInfo? ExtractPracticeInfo(DocumentReferenceData documentReference);

    /// <summary>
    /// Resolves practice name from practice ID (placeholder for Organization lookup)
    /// </summary>
    /// <param name="practiceId">The practice ID to resolve</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Practice name or default value</returns>
    Task<string> ResolvePracticeNameAsync(string practiceId, CancellationToken cancellationToken = default);
}