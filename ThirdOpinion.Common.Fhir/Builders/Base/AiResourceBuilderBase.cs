using Hl7.Fhir.Model;
using ThirdOpinion.Common.Fhir.Configuration;
using ThirdOpinion.Common.Fhir.Helpers;

namespace ThirdOpinion.Common.Fhir.Builders.Base;

/// <summary>
///     Abstract base class for building FHIR resources with AI inference metadata
/// </summary>
/// <typeparam name="T">The type of FHIR resource to build</typeparam>
/// <typeparam name="TBuilder">The derived builder type (for fluent interface)</typeparam>
public abstract class AiResourceBuilderBase<T, TBuilder>
    where T : Resource
    where TBuilder : AiResourceBuilderBase<T, TBuilder>
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
        Notes = new List<string>();
        FocusReferences = new List<ResourceReference>();
        EvidenceReferences = new List<ResourceReference>();
    }

    /// <summary>
    ///     The FHIR resource ID for this resource
    /// </summary>
    protected string? FhirResourceId { get; set; }

    /// <summary>
    ///     List of resources this inference was derived from
    /// </summary>
    protected List<ResourceReference> DerivedFromReferences { get; }

    /// <summary>
    ///     The patient reference for this resource
    /// </summary>
    protected ResourceReference? PatientReference { get; set; }

    /// <summary>
    ///     The device reference for this resource
    /// </summary>
    protected ResourceReference? DeviceReference { get; set; }

    /// <summary>
    ///     The AI confidence score for this resource
    /// </summary>
    protected float? Confidence { get; set; }

    /// <summary>
    ///     List of notes for this resource
    /// </summary>
    protected List<string> Notes { get; }

    /// <summary>
    ///     List of focus references for this resource (conditions/tumors/lesions being assessed)
    /// </summary>
    protected List<ResourceReference> FocusReferences { get; }

    /// <summary>
    ///     List of evidence references supporting this resource
    /// </summary>
    protected List<ResourceReference> EvidenceReferences { get; }

    /// <summary>
    ///     Configuration for AI inference operations
    /// </summary>
    protected AiInferenceConfiguration Configuration { get; }

    /// <summary>
    ///     Sets the FHIR resource ID for this resource
    /// </summary>
    /// <param name="id">The FHIR resource ID (will be prefixed with 'to.ai-' if not already present)</param>
    /// <returns>This builder instance for method chaining</returns>
    public TBuilder WithFhirResourceId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("FHIR resource ID cannot be null or empty", nameof(id));

        // Ensure the ID starts with 'to.ai-'
        FhirResourceId = id.StartsWith("to.ai-") ? id : $"to.ai-{id}";
        return (TBuilder)this;
    }

    /// <summary>
    ///     Adds a resource reference that this inference was derived from
    /// </summary>
    /// <param name="reference">The resource reference</param>
    /// <returns>This builder instance for method chaining</returns>
    public TBuilder AddDerivedFrom(ResourceReference reference)
    {
        if (reference != null) DerivedFromReferences.Add(reference);
        return (TBuilder)this;
    }

    /// <summary>
    ///     Adds a resource reference that this inference was derived from
    /// </summary>
    /// <param name="reference">The reference string (e.g., "Patient/123")</param>
    /// <param name="display">Optional display text for the reference</param>
    /// <returns>This builder instance for method chaining</returns>
    public TBuilder AddDerivedFrom(string reference, string? display = null)
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

        return (TBuilder)this;
    }

    /// <summary>
    ///     Sets the patient reference for this resource
    /// </summary>
    /// <param name="patient">The patient resource reference</param>
    /// <returns>This builder instance for method chaining</returns>
    public TBuilder WithPatient(ResourceReference patient)
    {
        PatientReference = patient ?? throw new ArgumentNullException(nameof(patient));
        return (TBuilder)this;
    }

    /// <summary>
    ///     Sets the patient reference for this resource
    /// </summary>
    /// <param name="patientId">The patient ID</param>
    /// <param name="display">Optional display text</param>
    /// <returns>This builder instance for method chaining</returns>
    public TBuilder WithPatient(string patientId, string? display = null)
    {
        if (string.IsNullOrWhiteSpace(patientId))
            throw new ArgumentException("Patient ID cannot be null or empty", nameof(patientId));

        PatientReference = new ResourceReference
        {
            Reference = patientId.StartsWith("Patient/") ? patientId : $"Patient/{patientId}",
            Display = display
        };
        return (TBuilder)this;
    }

    /// <summary>
    ///     Sets the device reference for this resource
    /// </summary>
    /// <param name="device">The device resource reference</param>
    /// <returns>This builder instance for method chaining</returns>
    public TBuilder WithDevice(ResourceReference device)
    {
        DeviceReference = device ?? throw new ArgumentNullException(nameof(device));
        return (TBuilder)this;
    }

    /// <summary>
    ///     Sets the device reference for this resource
    /// </summary>
    /// <param name="deviceId">The device ID</param>
    /// <param name="display">Optional display text</param>
    /// <returns>This builder instance for method chaining</returns>
    public TBuilder WithDevice(string deviceId, string? display = null)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            throw new ArgumentException("Device ID cannot be null or empty", nameof(deviceId));

        DeviceReference = new ResourceReference
        {
            Reference = deviceId.StartsWith("Device/") ? deviceId : $"Device/{deviceId}",
            Display = display
        };
        return (TBuilder)this;
    }

    /// <summary>
    ///     Sets the AI confidence score for this resource
    /// </summary>
    /// <param name="confidence">The confidence score (0.0 to 1.0)</param>
    /// <returns>This builder instance for method chaining</returns>
    public TBuilder WithConfidence(float confidence)
    {
        if (confidence < 0.0f || confidence > 1.0f)
            throw new ArgumentOutOfRangeException(nameof(confidence),
                "Confidence must be between 0.0 and 1.0");

        Confidence = confidence;
        return (TBuilder)this;
    }

    /// <summary>
    ///     Adds a note to this resource
    /// </summary>
    /// <param name="noteText">The note text</param>
    /// <returns>This builder instance for method chaining</returns>
    public TBuilder AddNote(string noteText)
    {
        if (!string.IsNullOrWhiteSpace(noteText)) Notes.Add(noteText);
        return (TBuilder)this;
    }

    /// <summary>
    ///     Sets the focus references for this resource (conditions/tumors/lesions being assessed)
    /// </summary>
    /// <param name="focuses">The focus resource references</param>
    /// <returns>This builder instance for method chaining</returns>
    public TBuilder WithFocus(params ResourceReference[] focuses)
    {
        if (focuses == null || focuses.Length == 0)
            throw new ArgumentException("At least one focus reference is required", nameof(focuses));

        FocusReferences.Clear();
        FocusReferences.AddRange(focuses.Where(f => f != null));
        return (TBuilder)this;
    }

    /// <summary>
    ///     Adds evidence supporting this resource
    /// </summary>
    /// <param name="reference">The evidence resource reference</param>
    /// <param name="display">Optional display text for the evidence</param>
    /// <returns>This builder instance for method chaining</returns>
    public TBuilder AddEvidence(ResourceReference reference, string? display = null)
    {
        if (reference != null)
        {
            if (!string.IsNullOrWhiteSpace(display) && string.IsNullOrWhiteSpace(reference.Display))
                reference.Display = display;
            EvidenceReferences.Add(reference);
        }

        return (TBuilder)this;
    }

    /// <summary>
    ///     Adds evidence supporting this resource
    /// </summary>
    /// <param name="referenceString">The evidence reference string (e.g., "DocumentReference/123")</param>
    /// <param name="display">Optional display text for the evidence</param>
    /// <returns>This builder instance for method chaining</returns>
    public TBuilder AddEvidence(string referenceString, string? display = null)
    {
        if (!string.IsNullOrWhiteSpace(referenceString))
        {
            var reference = new ResourceReference
            {
                Reference = referenceString,
                Display = display
            };
            EvidenceReferences.Add(reference);
        }

        return (TBuilder)this;
    }

    /// <summary>
    ///     Ensures a FHIR resource ID is set and starts with 'to.ai-', generating one if necessary
    /// </summary>
    protected void EnsureFhirResourceId()
    {
        if (string.IsNullOrWhiteSpace(FhirResourceId))
            lock (_idGenerationLock)
            {
                // Double-check after acquiring lock
                if (string.IsNullOrWhiteSpace(FhirResourceId))
                {
                    var generatedId = FhirIdGenerator.GenerateInferenceId();
                    // Ensure the generated ID starts with 'to.ai-'
                    FhirResourceId = generatedId.StartsWith("to.ai-") ? generatedId : $"to.ai-{generatedId}";
                }
            }

        // Validate that the ID starts with 'to.ai-' even if already set
        if (!FhirResourceId!.StartsWith("to.ai-"))
            throw new InvalidOperationException(
                $"FHIR resource ID must start with 'to.ai-'. Current ID: {FhirResourceId}");
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
        // Validate core required fields (PatientReference and DeviceReference are always required)
        if (PatientReference == null)
            throw new InvalidOperationException(
                "Patient reference is required. Call WithPatient() before Build().");

        if (DeviceReference == null)
            throw new InvalidOperationException(
                "Device reference is required. Call WithDevice() before Build().");

        // Notes is NOT required (as per user request)
        // Other fields (FhirResourceId, DerivedFromReferences, Confidence, FocusReferences, EvidenceReferences)
        // can be validated by derived classes if they require them
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

        // Ensure we have a FHIR resource ID
        EnsureFhirResourceId();

        // Call derived class build logic
        T resource = BuildCore();

        // Apply AIAST security label
        ApplyAiastSecurityLabel(resource);

        // Set the resource ID if it's not already set
        if (string.IsNullOrWhiteSpace(resource.Id)) resource.Id = FhirResourceId;

        return resource;
    }
}