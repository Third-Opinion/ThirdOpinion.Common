using Hl7.Fhir.Model;
using ThirdOpinion.Common.Fhir.Builders.Base;
using ThirdOpinion.Common.Fhir.Configuration;
using ThirdOpinion.Common.Fhir.Helpers;

namespace ThirdOpinion.Common.Fhir.Builders.Provenance;

public class AiProvenanceBuilder : AiResourceBuilderBase<Hl7.Fhir.Model.Provenance>
{
    private string? _provenanceId;
    private List<ResourceReference> _targets = new();
    private DateTimeOffset? _occurredDateTime;
    private DateTimeOffset? _recordedDateTime;
    private List<string> _reasons = new();
    private List<Hl7.Fhir.Model.Provenance.AgentComponent> _agents = new();
    private List<Hl7.Fhir.Model.Provenance.EntityComponent> _entities = new();
    private string? _s3LogFileUrl;

    public AiProvenanceBuilder() : base(AiInferenceConfiguration.CreateDefault())
    {
    }

    public AiProvenanceBuilder(AiInferenceConfiguration configuration) : base(configuration)
    {
    }

    public AiProvenanceBuilder WithProvenanceId(string provenanceId)
    {
        if (string.IsNullOrWhiteSpace(provenanceId))
            throw new ArgumentException("Provenance ID cannot be null, empty, or whitespace", nameof(provenanceId));
        _provenanceId = provenanceId;
        return this;
    }

    public AiProvenanceBuilder ForTarget(ResourceReference target)
    {
        ArgumentNullException.ThrowIfNull(target);
        _targets.Add(target);
        return this;
    }

    public AiProvenanceBuilder ForTarget(string resourceType, string resourceId)
    {
        ArgumentException.ThrowIfNullOrEmpty(resourceType);
        ArgumentException.ThrowIfNullOrEmpty(resourceId);

        var reference = new ResourceReference($"{resourceType}/{resourceId}");
        return ForTarget(reference);
    }

    public AiProvenanceBuilder WithOccurredDateTime(DateTimeOffset occurredDateTime)
    {
        _occurredDateTime = occurredDateTime;
        return this;
    }

    public AiProvenanceBuilder WithRecordedDateTime(DateTimeOffset recordedDateTime)
    {
        _recordedDateTime = recordedDateTime;
        return this;
    }

    public AiProvenanceBuilder WithReason(string reason)
    {
        ArgumentException.ThrowIfNullOrEmpty(reason);
        _reasons.Add(reason);
        return this;
    }

    public AiProvenanceBuilder WithAgent(string agentType, string agentName, string? agentVersion = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(agentType);
        ArgumentException.ThrowIfNullOrEmpty(agentName);

        var agent = new Hl7.Fhir.Model.Provenance.AgentComponent
        {
            Type = FhirCodingHelper.CreateSnomedConcept(FhirCodingHelper.SnomedCodes.AI_ALGORITHM, "AI Algorithm"),
            Who = new ResourceReference
            {
                Display = agentName
            }
        };

        if (!string.IsNullOrEmpty(agentVersion))
        {
            agent.Who.Extension = new List<Extension>
            {
                new Extension("http://hl7.org/fhir/StructureDefinition/device-softwareVersion", new FhirString(agentVersion))
            };
        }

        _agents.Add(agent);
        return this;
    }

    public AiProvenanceBuilder WithOrganization(string organizationName, string? organizationId = null)
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
        {
            agent.Who.Reference = $"Organization/{organizationId}";
        }

        _agents.Add(agent);
        return this;
    }

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

    public AiProvenanceBuilder WithSourceEntity(string resourceType, string resourceId)
    {
        ArgumentException.ThrowIfNullOrEmpty(resourceType);
        ArgumentException.ThrowIfNullOrEmpty(resourceId);

        var reference = new ResourceReference($"{resourceType}/{resourceId}");
        return WithSourceEntity(reference);
    }

    public AiProvenanceBuilder WithS3LogFile(string s3Url)
    {
        ArgumentException.ThrowIfNullOrEmpty(s3Url);

        if (!s3Url.StartsWith("s3://") && !s3Url.StartsWith("https://"))
        {
            throw new ArgumentException("S3 URL must start with 's3://' or 'https://'", nameof(s3Url));
        }

        _s3LogFileUrl = s3Url;
        return this;
    }

    protected override Hl7.Fhir.Model.Provenance BuildCore()
    {
        var provenance = new Hl7.Fhir.Model.Provenance
        {
            Id = _provenanceId,
            Target = _targets,
            Occurred = _occurredDateTime.HasValue ? new FhirDateTime(_occurredDateTime.Value) : null,
            Recorded = _recordedDateTime ?? DateTimeOffset.Now,
            Agent = _agents,
            Entity = _entities
        };

        if (_reasons.Any())
        {
            provenance.Reason = _reasons.Select(reason =>
                FhirCodingHelper.CreateSnomedConcept(FhirCodingHelper.SnomedCodes.AI_ALGORITHM, reason)).ToList();
        }

        if (!string.IsNullOrEmpty(_s3LogFileUrl))
        {
            provenance.Extension = new List<Extension>
            {
                new Extension("http://thirdopinion.ai/fhir/StructureDefinition/s3-log-file", new FhirUri(_s3LogFileUrl))
            };
        }

        return provenance;
    }

    protected override void ValidateRequiredFields()
    {
        var errors = new List<string>();

        if (_targets.Count == 0)
            errors.Add("At least one target resource must be specified");

        if (_agents.Count == 0)
            errors.Add("At least one agent must be specified");

        if (errors.Count > 0)
        {
            throw new InvalidOperationException($"Validation failed: {string.Join(", ", errors)}");
        }
    }
}