using Hl7.Fhir.Model;
using ThirdOpinion.Common.Fhir.Configuration;
using ThirdOpinion.Common.Fhir.Helpers;

namespace ThirdOpinion.Common.Fhir.Builders.Base;

/// <summary>
///     Abstract base class for building FHIR resources with AI inference metadata
/// </summary>
/// <typeparam name="T">The type of FHIR resource to build</typeparam>
public abstract class AiResourceBuilderBase<T> where T : Resource
{
    private static readonly object _idGenerationLock = new();

    /// <summary>
    ///     Creates a new instance of the builder with configuration
    /// </summary>
    /// <param name="configuration">The AI inference configuration</param>
    protected AiResourceBuilderBase(AiInferenceConfiguration configuration)
    {
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        DerivedFromReferences = new List<ResourceReference>();
    }

    /// <summary>
    ///     The inference ID for this resource
    /// </summary>
    protected string? InferenceId { get; set; }

    /// <summary>
    ///     The criteria ID used for this inference
    /// </summary>
    protected string? CriteriaId { get; set; }

    /// <summary>
    ///     The display text for the criteria
    /// </summary>
    protected string? CriteriaDisplay { get; set; }

    /// <summary>
    ///     The criteria system URI
    /// </summary>
    protected string? CriteriaSystem { get; set; }

    /// <summary>
    ///     List of resources this inference was derived from
    /// </summary>
    protected List<ResourceReference> DerivedFromReferences { get; }

    /// <summary>
    ///     Configuration for AI inference operations
    /// </summary>
    protected AiInferenceConfiguration Configuration { get; }

    /// <summary>
    ///     Sets the inference ID for this resource
    /// </summary>
    /// <param name="id">The inference ID</param>
    /// <returns>This builder instance for method chaining</returns>
    public AiResourceBuilderBase<T> WithInferenceId(string id)
    {
        InferenceId = id;
        return this;
    }

    /// <summary>
    ///     Sets the criteria information for this inference
    /// </summary>
    /// <param name="id">The criteria ID</param>
    /// <param name="display">The display text for the criteria</param>
    /// <param name="system">The criteria system URI (optional, uses configuration default if not provided)</param>
    /// <returns>This builder instance for method chaining</returns>
    public AiResourceBuilderBase<T> WithCriteria(string id, string display, string? system = null)
    {
        CriteriaId = id;
        CriteriaDisplay = display;
        CriteriaSystem = system ?? Configuration.CriteriaSystem;
        return this;
    }

    /// <summary>
    ///     Adds a resource reference that this inference was derived from
    /// </summary>
    /// <param name="reference">The resource reference</param>
    /// <returns>This builder instance for method chaining</returns>
    public AiResourceBuilderBase<T> AddDerivedFrom(ResourceReference reference)
    {
        if (reference != null) DerivedFromReferences.Add(reference);
        return this;
    }

    /// <summary>
    ///     Adds a resource reference that this inference was derived from
    /// </summary>
    /// <param name="reference">The reference string (e.g., "Patient/123")</param>
    /// <param name="display">Optional display text for the reference</param>
    /// <returns>This builder instance for method chaining</returns>
    public AiResourceBuilderBase<T> AddDerivedFrom(string reference, string? display = null)
    {
        if (!string.IsNullOrWhiteSpace(reference))
        {
            var resourceRef = new ResourceReference
            {
                Reference = reference,
                Display = display
            };
            DerivedFromReferences.Add(resourceRef);
        }

        return this;
    }

    /// <summary>
    ///     Ensures an inference ID is set, generating one if necessary
    /// </summary>
    protected void EnsureInferenceId()
    {
        if (string.IsNullOrWhiteSpace(InferenceId))
            lock (_idGenerationLock)
            {
                // Double-check after acquiring lock
                if (string.IsNullOrWhiteSpace(InferenceId))
                    InferenceId = FhirIdGenerator.GenerateInferenceId();
            }
    }

    /// <summary>
    ///     Applies the AIAST security label to a resource
    /// </summary>
    /// <param name="resource">The resource to apply the label to</param>
    protected void ApplyAiastSecurityLabel(T resource)
    {
        if (resource == null) return;

        // Ensure Meta exists
        resource.Meta ??= new Meta();

        // Ensure Security list exists
        resource.Meta.Security ??= new List<Coding>();

        // Add AIAST security label
        var aiastLabel = new Coding
        {
            System = "http://terminology.hl7.org/CodeSystem/v3-ActCode",
            Code = "AIAST",
            Display = "AI Assisted"
        };

        // Only add if not already present
        if (!resource.Meta.Security.Any(s =>
                s.System == aiastLabel.System && s.Code == aiastLabel.Code))
            resource.Meta.Security.Add(aiastLabel);
    }

    /// <summary>
    ///     Validates that required fields are set
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when required fields are missing</exception>
    protected virtual void ValidateRequiredFields()
    {
        // Base implementation - derived classes can override to add specific validations
    }

    /// <summary>
    ///     Core build logic to be implemented by derived classes
    /// </summary>
    /// <returns>The built FHIR resource</returns>
    protected abstract T BuildCore();

    /// <summary>
    ///     Builds the FHIR resource with AI metadata
    /// </summary>
    /// <returns>The completed FHIR resource</returns>
    public T Build()
    {
        // Validate required fields
        ValidateRequiredFields();

        // Ensure we have an inference ID
        EnsureInferenceId();

        // Call derived class build logic
        T resource = BuildCore();

        // Apply AIAST security label
        ApplyAiastSecurityLabel(resource);

        // Set the resource ID if it's not already set
        if (string.IsNullOrWhiteSpace(resource.Id)) resource.Id = InferenceId;

        return resource;
    }
}