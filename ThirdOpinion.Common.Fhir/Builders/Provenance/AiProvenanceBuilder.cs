using Hl7.Fhir.Model;
using ThirdOpinion.Common.Fhir.Builders.Base;
using ThirdOpinion.Common.Fhir.Configuration;
using ThirdOpinion.Common.Fhir.Helpers;

namespace ThirdOpinion.Common.Fhir.Builders.Provenance;

/// <summary>
///     Builder for creating FHIR Provenance resources specifically for AI inference operations.
///     Provides fluent API for configuring provenance tracking of AI-generated clinical data.
/// </summary>
public class AiProvenanceBuilder : AiResourceBuilderBase<Hl7.Fhir.Model.Provenance, AiProvenanceBuilder>
{
    private readonly List<Hl7.Fhir.Model.Provenance.AgentComponent> _agents = new();
    private readonly List<Hl7.Fhir.Model.Provenance.EntityComponent> _entities = new();
    private readonly List<string> _reasons = new();
    private readonly List<ResourceReference> _targets = new();
    private DateTimeOffset? _occurredDateTime;
    private string? _provenanceId;
    private DateTimeOffset? _recordedDateTime;
    private string? _s3LogFileUrl;

    /// <summary>
    ///     Initializes a new instance of the AiProvenanceBuilder class with default configuration.
    /// </summary>
    public AiProvenanceBuilder() : base(AiInferenceConfiguration.CreateDefault())
    {
    }

    /// <summary>
    ///     Initializes a new instance of the AiProvenanceBuilder class with the specified configuration.
    /// </summary>
    /// <param name="configuration">The AI inference configuration to use for building the provenance resource.</param>
    public AiProvenanceBuilder(AiInferenceConfiguration configuration) : base(configuration)
    {
    }

    /// <summary>
    ///     Sets the unique identifier for the provenance resource.
    /// </summary>
    /// <param name="provenanceId">The unique identifier for the provenance resource.</param>
    /// <returns>The current AiProvenanceBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when provenanceId is null, empty, or whitespace.</exception>
    public AiProvenanceBuilder WithProvenanceId(string provenanceId)
    {
        if (string.IsNullOrWhiteSpace(provenanceId))
            throw new ArgumentException("Provenance ID cannot be null, empty, or whitespace",
                nameof(provenanceId));
        // Use the base class method to ensure 'to.ai-' prefix is applied
        WithFhirResourceId(provenanceId);
        _provenanceId = FhirResourceId;
        return this;
    }

    /// <summary>
    ///     Adds a target resource reference that this provenance tracks.
    /// </summary>
    /// <param name="target">The resource reference to track provenance for.</param>
    /// <returns>The current AiProvenanceBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when target is null.</exception>
    public AiProvenanceBuilder ForTarget(ResourceReference target)
    {
        ArgumentNullException.ThrowIfNull(target);
        _targets.Add(target);
        return this;
    }

    /// <summary>
    ///     Adds a target resource reference using resource type and ID.
    /// </summary>
    /// <param name="resourceType">The type of the target resource (e.g., "Patient", "Observation").</param>
    /// <param name="resourceId">The unique identifier of the target resource.</param>
    /// <returns>The current AiProvenanceBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when resourceType or resourceId is null or empty.</exception>
    public AiProvenanceBuilder ForTarget(string resourceType, string resourceId)
    {
        ArgumentException.ThrowIfNullOrEmpty(resourceType);
        ArgumentException.ThrowIfNullOrEmpty(resourceId);

        var reference = new ResourceReference($"{resourceType}/{resourceId}");
        return ForTarget(reference);
    }

    /// <summary>
    ///     Sets the date and time when the activity that is being documented occurred.
    /// </summary>
    /// <param name="occurredDateTime">The date and time when the AI inference occurred.</param>
    /// <returns>The current AiProvenanceBuilder instance for method chaining.</returns>
    public AiProvenanceBuilder WithOccurredDateTime(DateTimeOffset occurredDateTime)
    {
        _occurredDateTime = occurredDateTime;
        return this;
    }

    /// <summary>
    ///     Sets the date and time when the provenance was recorded.
    /// </summary>
    /// <param name="recordedDateTime">The date and time when the provenance was recorded.</param>
    /// <returns>The current AiProvenanceBuilder instance for method chaining.</returns>
    public AiProvenanceBuilder WithRecordedDateTime(DateTimeOffset recordedDateTime)
    {
        _recordedDateTime = recordedDateTime;
        return this;
    }

    /// <summary>
    ///     Adds a reason for the activity that is being documented.
    /// </summary>
    /// <param name="reason">The reason for the AI inference activity.</param>
    /// <returns>The current AiProvenanceBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when reason is null or empty.</exception>
    public AiProvenanceBuilder WithReason(string reason)
    {
        ArgumentException.ThrowIfNullOrEmpty(reason);
        _reasons.Add(reason);
        return this;
    }

    /// <summary>
    ///     Adds an AI agent that participated in the activity being documented.
    /// </summary>
    /// <param name="agentType">The type of the agent (e.g., "AI Algorithm").</param>
    /// <param name="agentName">The display name of the AI agent.</param>
    /// <param name="agentVersion">The optional version of the AI agent software.</param>
    /// <returns>The current AiProvenanceBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when agentType or agentName is null or empty.</exception>
    public AiProvenanceBuilder WithAgent(string agentType,
        string agentName,
        string? agentVersion = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(agentType);
        ArgumentException.ThrowIfNullOrEmpty(agentName);

        var agent = new Hl7.Fhir.Model.Provenance.AgentComponent
        {
            Type = FhirCodingHelper.CreateSnomedConcept(FhirCodingHelper.SnomedCodes.AI_ALGORITHM,
                "AI Algorithm"),
            Who = new ResourceReference
            {
                Display = agentName
            }
        };

        if (!string.IsNullOrEmpty(agentVersion))
            agent.Who.Extension = new List<Extension>
            {
                new("http://hl7.org/fhir/StructureDefinition/device-softwareVersion",
                    new FhirString(agentVersion))
            };

        _agents.Add(agent);
        return this;
    }

    /// <summary>
    ///     Adds an organization that participated in or is responsible for the activity being documented.
    /// </summary>
    /// <param name="organizationName">The display name of the organization.</param>
    /// <param name="organizationId">The optional unique identifier of the organization resource.</param>
    /// <returns>The current AiProvenanceBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when organizationName is null or empty.</exception>
    public AiProvenanceBuilder WithOrganization(string organizationName,
        string? organizationId = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(organizationName);

        var agent = new Hl7.Fhir.Model.Provenance.AgentComponent
        {
            Type = FhirCodingHelper.CreateSnomedConcept("385437003", "Organization"),
            Who = new ResourceReference
            {
                Display = organizationName
            }
        };

        if (!string.IsNullOrEmpty(organizationId))
            agent.Who.Reference = $"Organization/{organizationId}";

        _agents.Add(agent);
        return this;
    }

    /// <summary>
    ///     Adds a source entity that was used to create the target resource.
    /// </summary>
    /// <param name="sourceReference">The resource reference to the source entity.</param>
    /// <returns>The current AiProvenanceBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when sourceReference is null.</exception>
    public AiProvenanceBuilder WithSourceEntity(ResourceReference sourceReference)
    {
        ArgumentNullException.ThrowIfNull(sourceReference);

        var entity = new Hl7.Fhir.Model.Provenance.EntityComponent
        {
            Role = Hl7.Fhir.Model.Provenance.ProvenanceEntityRole.Source,
            What = sourceReference
        };

        _entities.Add(entity);
        return this;
    }

    /// <summary>
    ///     Adds a source entity using resource type and ID that was used to create the target resource.
    /// </summary>
    /// <param name="resourceType">The type of the source resource (e.g., "DocumentReference", "DiagnosticReport").</param>
    /// <param name="resourceId">The unique identifier of the source resource.</param>
    /// <returns>The current AiProvenanceBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when resourceType or resourceId is null or empty.</exception>
    public AiProvenanceBuilder WithSourceEntity(string resourceType, string resourceId)
    {
        ArgumentException.ThrowIfNullOrEmpty(resourceType);
        ArgumentException.ThrowIfNullOrEmpty(resourceId);

        var reference = new ResourceReference($"{resourceType}/{resourceId}");
        return WithSourceEntity(reference);
    }

    /// <summary>
    ///     Adds an S3 URL reference to the log file containing detailed information about the AI inference process.
    /// </summary>
    /// <param name="s3Url">The S3 URL to the log file. Must start with 's3://' or 'https://'.</param>
    /// <returns>The current AiProvenanceBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when s3Url is null, empty, or doesn't start with 's3://' or 'https://'.</exception>
    public AiProvenanceBuilder WithS3LogFile(string s3Url)
    {
        ArgumentException.ThrowIfNullOrEmpty(s3Url);

        if (!s3Url.StartsWith("s3://") && !s3Url.StartsWith("https://"))
            throw new ArgumentException("S3 URL must start with 's3://' or 'https://'",
                nameof(s3Url));

        _s3LogFileUrl = s3Url;
        return this;
    }

    /// <summary>
    ///     Builds the FHIR Provenance resource with the configured properties.
    /// </summary>
    /// <returns>A configured FHIR Provenance resource.</returns>
    protected override Hl7.Fhir.Model.Provenance BuildCore()
    {
        var provenance = new Hl7.Fhir.Model.Provenance
        {
            Id = _provenanceId,
            Target = _targets,
            Occurred
                = _occurredDateTime.HasValue ? new FhirDateTime(_occurredDateTime.Value) : null,
            Recorded = _recordedDateTime ?? DateTimeOffset.Now,
            Agent = _agents,
            Entity = _entities
        };

        if (_reasons.Any())
            provenance.Reason = _reasons.Select(reason =>
                FhirCodingHelper.CreateSnomedConcept(FhirCodingHelper.SnomedCodes.AI_ALGORITHM,
                    reason)).ToList();

        if (!string.IsNullOrEmpty(_s3LogFileUrl))
            provenance.Extension = new List<Extension>
            {
                new("http://thirdopinion.ai/fhir/StructureDefinition/s3-log-file",
                    new FhirUri(_s3LogFileUrl))
            };

        return provenance;
    }

    /// <summary>
    ///     Validates that all required fields have been configured before building the resource.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when required fields are missing.</exception>
    protected override void ValidateRequiredFields()
    {
        var errors = new List<string>();

        if (_targets.Count == 0)
            errors.Add("At least one target resource must be specified");

        if (_agents.Count == 0)
            errors.Add("At least one agent must be specified");

        if (errors.Count > 0)
            throw new InvalidOperationException($"Validation failed: {string.Join(", ", errors)}");
    }
}