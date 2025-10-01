using Hl7.Fhir.Model;

namespace ThirdOpinion.Common.Fhir.Configuration;

/// <summary>
/// Configuration class holding system references and default settings for AI inference operations
/// </summary>
public class AiInferenceConfiguration
{
    /// <summary>
    /// URI for the inference code system
    /// </summary>
    public string InferenceSystem { get; set; }

    /// <summary>
    /// URI for the criteria code system
    /// </summary>
    public string CriteriaSystem { get; set; }

    /// <summary>
    /// URI for the model code system
    /// </summary>
    public string ModelSystem { get; set; }

    /// <summary>
    /// URI for the document tracking system
    /// </summary>
    public string DocumentTrackingSystem { get; set; }

    /// <summary>
    /// URI for the provenance system
    /// </summary>
    public string ProvenanceSystem { get; set; }

    /// <summary>
    /// Default model version (e.g., 'v1.0')
    /// </summary>
    public string DefaultModelVersion { get; set; }

    /// <summary>
    /// Reference to the organization performing the inference
    /// </summary>
    public ResourceReference? OrganizationReference { get; set; }

    /// <summary>
    /// Creates a new instance of AiInferenceConfiguration
    /// </summary>
    public AiInferenceConfiguration()
    {
        // Default URIs - these can be overridden
        InferenceSystem = "http://thirdopinion.ai/fhir/CodeSystem/inference";
        CriteriaSystem = "http://thirdopinion.ai/fhir/CodeSystem/criteria";
        ModelSystem = "http://thirdopinion.ai/fhir/CodeSystem/model";
        DocumentTrackingSystem = "http://thirdopinion.ai/fhir/CodeSystem/document-tracking";
        ProvenanceSystem = "http://thirdopinion.ai/fhir/CodeSystem/provenance";
        DefaultModelVersion = "v1.0";
    }

    /// <summary>
    /// Creates a new instance with all required values
    /// </summary>
    /// <param name="inferenceSystem">URI for inference system</param>
    /// <param name="criteriaSystem">URI for criteria system</param>
    /// <param name="modelSystem">URI for model system</param>
    /// <param name="documentTrackingSystem">URI for document tracking</param>
    /// <param name="provenanceSystem">URI for provenance system</param>
    /// <param name="defaultModelVersion">Default model version</param>
    /// <param name="organizationReference">Organization reference</param>
    public AiInferenceConfiguration(
        string inferenceSystem,
        string criteriaSystem,
        string modelSystem,
        string documentTrackingSystem,
        string provenanceSystem,
        string defaultModelVersion,
        ResourceReference? organizationReference = null)
    {
        InferenceSystem = ValidateUri(inferenceSystem, nameof(inferenceSystem));
        CriteriaSystem = ValidateUri(criteriaSystem, nameof(criteriaSystem));
        ModelSystem = ValidateUri(modelSystem, nameof(modelSystem));
        DocumentTrackingSystem = ValidateUri(documentTrackingSystem, nameof(documentTrackingSystem));
        ProvenanceSystem = ValidateUri(provenanceSystem, nameof(provenanceSystem));
        DefaultModelVersion = defaultModelVersion ?? throw new ArgumentNullException(nameof(defaultModelVersion));
        OrganizationReference = organizationReference;
    }

    /// <summary>
    /// Validates that a string is a valid URI format
    /// </summary>
    /// <param name="uri">The URI string to validate</param>
    /// <param name="parameterName">The parameter name for error messages</param>
    /// <returns>The validated URI string</returns>
    private static string ValidateUri(string uri, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            throw new ArgumentException($"URI cannot be null or empty", parameterName);
        }

        if (!Uri.IsWellFormedUriString(uri, UriKind.Absolute))
        {
            throw new ArgumentException($"'{uri}' is not a valid absolute URI", parameterName);
        }

        return uri;
    }

    /// <summary>
    /// Creates a default configuration for Third Opinion AI inference
    /// </summary>
    /// <returns>A new AiInferenceConfiguration with default values</returns>
    public static AiInferenceConfiguration CreateDefault()
    {
        return new AiInferenceConfiguration
        {
            OrganizationReference = new ResourceReference
            {
                Reference = "Organization/thirdopinion-ai",
                Display = "Third Opinion AI"
            }
        };
    }
}