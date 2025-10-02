using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using ThirdOpinion.Common.Fhir.Builders.Provenance;
using ThirdOpinion.Common.Fhir.Helpers;

namespace ThirdOpinion.Common.Fhir.UnitTests.Builders.Provenance;

public class AiProvenanceBuilderTests
{
    [Fact]
    public void Build_WithValidData_CreatesProvenanceResource()
    {
        var target = new ResourceReference("DocumentReference/test-doc-123");
        var builder = new AiProvenanceBuilder()
            .WithProvenanceId("prov-123")
            .ForTarget(target)
            .WithAgent("ai-algorithm", "TestAI", "1.0.0")
            .WithOrganization("ThirdOpinion", "org-123")
            .WithOccurredDateTime(new DateTimeOffset(2024, 10, 1, 10, 0, 0, TimeSpan.Zero))
            .WithRecordedDateTime(new DateTimeOffset(2024, 10, 1, 10, 5, 0, TimeSpan.Zero))
            .WithReason("AI-assisted analysis")
            .WithSourceEntity("Document", "source-doc-123")
            .WithS3LogFile("s3://bucket/logs/process.log");

        var provenance = builder.Build();

        provenance.ShouldNotBeNull();
        provenance.Id.ShouldBe("prov-123");
        provenance.Target.Count.ShouldBe(1);
        provenance.Target[0].Reference.ShouldBe("DocumentReference/test-doc-123");
        provenance.Agent.Count.ShouldBe(2);
        provenance.Entity.Count.ShouldBe(1);
        provenance.Extension.Count.ShouldBe(1);
        provenance.Extension[0].Url.ShouldBe("http://thirdopinion.ai/fhir/StructureDefinition/s3-log-file");
    }

    [Fact]
    public void Build_WithoutTargets_ThrowsInvalidOperationException()
    {
        var builder = new AiProvenanceBuilder()
            .WithAgent("ai-algorithm", "TestAI");

        var exception = Should.Throw<InvalidOperationException>(() => builder.Build());
        exception.Message.ShouldContain("At least one target resource must be specified");
    }

    [Fact]
    public void Build_WithoutAgents_ThrowsInvalidOperationException()
    {
        var builder = new AiProvenanceBuilder()
            .ForTarget("DocumentReference", "test-doc");

        var exception = Should.Throw<InvalidOperationException>(() => builder.Build());
        exception.Message.ShouldContain("At least one agent must be specified");
    }

    [Fact]
    public void WithProvenanceId_WithNullOrEmptyId_ThrowsArgumentException()
    {
        var builder = new AiProvenanceBuilder();

        Should.Throw<ArgumentException>(() => builder.WithProvenanceId(null!));
        Should.Throw<ArgumentException>(() => builder.WithProvenanceId(""));
        Should.Throw<ArgumentException>(() => builder.WithProvenanceId("   "));
    }

    [Fact]
    public void ForTarget_WithResourceReference_AddsTarget()
    {
        var target = new ResourceReference("DocumentReference/test-doc");
        var builder = new AiProvenanceBuilder()
            .ForTarget(target)
            .WithAgent("ai-algorithm", "TestAI");

        var provenance = builder.Build();
        provenance.Target.Count.ShouldBe(1);
        provenance.Target[0].Reference.ShouldBe("DocumentReference/test-doc");
    }

    [Fact]
    public void ForTarget_WithResourceTypeAndId_AddsTarget()
    {
        var builder = new AiProvenanceBuilder()
            .ForTarget("DocumentReference", "test-doc")
            .WithAgent("ai-algorithm", "TestAI");

        var provenance = builder.Build();
        provenance.Target.Count.ShouldBe(1);
        provenance.Target[0].Reference.ShouldBe("DocumentReference/test-doc");
    }

    [Fact]
    public void ForTarget_WithMultipleTargets_AddsAllTargets()
    {
        var builder = new AiProvenanceBuilder()
            .ForTarget("DocumentReference", "doc-1")
            .ForTarget("Observation", "obs-1")
            .WithAgent("ai-algorithm", "TestAI");

        var provenance = builder.Build();
        provenance.Target.Count.ShouldBe(2);
        provenance.Target[0].Reference.ShouldBe("DocumentReference/doc-1");
        provenance.Target[1].Reference.ShouldBe("Observation/obs-1");
    }

    [Fact]
    public void WithAgent_WithValidData_AddsAiAgent()
    {
        var builder = new AiProvenanceBuilder()
            .ForTarget("DocumentReference", "test")
            .WithAgent("ai-algorithm", "TestAI", "1.0.0");

        var provenance = builder.Build();
        provenance.Agent.Count.ShouldBe(1);

        var agent = provenance.Agent[0];
        agent.Type.Coding[0].Code.ShouldBe(FhirCodingHelper.SnomedCodes.AI_ALGORITHM);
        agent.Who.Display.ShouldBe("TestAI");
        agent.Who.Extension.Count.ShouldBe(1);
        agent.Who.Extension[0].Url.ShouldBe("http://hl7.org/fhir/StructureDefinition/device-softwareVersion");
    }

    [Fact]
    public void WithAgent_WithoutVersion_AddsAgentWithoutExtension()
    {
        var builder = new AiProvenanceBuilder()
            .ForTarget("DocumentReference", "test")
            .WithAgent("ai-algorithm", "TestAI");

        var provenance = builder.Build();
        var agent = provenance.Agent[0];
        (agent.Who.Extension?.Count ?? 0).ShouldBe(0);
    }

    [Fact]
    public void WithOrganization_WithValidData_AddsOrganizationAgent()
    {
        var builder = new AiProvenanceBuilder()
            .ForTarget("DocumentReference", "test")
            .WithOrganization("ThirdOpinion", "org-123");

        var provenance = builder.Build();
        provenance.Agent.Count.ShouldBe(1);

        var agent = provenance.Agent[0];
        agent.Type.Coding[0].Code.ShouldBe("385437003");
        agent.Who.Display.ShouldBe("ThirdOpinion");
        agent.Who.Reference.ShouldBe("Organization/org-123");
    }

    [Fact]
    public void WithOrganization_WithoutId_AddsOrganizationWithoutReference()
    {
        var builder = new AiProvenanceBuilder()
            .ForTarget("DocumentReference", "test")
            .WithOrganization("ThirdOpinion");

        var provenance = builder.Build();
        var agent = provenance.Agent[0];
        agent.Who.Display.ShouldBe("ThirdOpinion");
        agent.Who.Reference.ShouldBeNull();
    }

    [Fact]
    public void WithSourceEntity_WithResourceReference_AddsEntity()
    {
        var sourceRef = new ResourceReference("Document/source-123");
        var builder = new AiProvenanceBuilder()
            .ForTarget("DocumentReference", "test")
            .WithAgent("ai-algorithm", "TestAI")
            .WithSourceEntity(sourceRef);

        var provenance = builder.Build();
        provenance.Entity.Count.ShouldBe(1);

        var entity = provenance.Entity[0];
        entity.Role.ShouldBe(Hl7.Fhir.Model.Provenance.ProvenanceEntityRole.Source);
        entity.What.Reference.ShouldBe("Document/source-123");
    }

    [Fact]
    public void WithSourceEntity_WithResourceTypeAndId_AddsEntity()
    {
        var builder = new AiProvenanceBuilder()
            .ForTarget("DocumentReference", "test")
            .WithAgent("ai-algorithm", "TestAI")
            .WithSourceEntity("Document", "source-123");

        var provenance = builder.Build();
        var entity = provenance.Entity[0];
        entity.What.Reference.ShouldBe("Document/source-123");
    }

    [Fact]
    public void WithS3LogFile_WithValidS3Url_AddsExtension()
    {
        var builder = new AiProvenanceBuilder()
            .ForTarget("DocumentReference", "test")
            .WithAgent("ai-algorithm", "TestAI")
            .WithS3LogFile("s3://bucket/logs/process.log");

        var provenance = builder.Build();
        provenance.Extension.Count.ShouldBe(1);
        provenance.Extension[0].Url.ShouldBe("http://thirdopinion.ai/fhir/StructureDefinition/s3-log-file");
        ((FhirUri)provenance.Extension[0].Value).Value.ShouldBe("s3://bucket/logs/process.log");
    }

    [Fact]
    public void WithS3LogFile_WithHttpsUrl_AddsExtension()
    {
        var builder = new AiProvenanceBuilder()
            .ForTarget("DocumentReference", "test")
            .WithAgent("ai-algorithm", "TestAI")
            .WithS3LogFile("https://bucket.s3.amazonaws.com/logs/process.log");

        var provenance = builder.Build();
        var extension = provenance.Extension[0];
        ((FhirUri)extension.Value).Value.ShouldBe("https://bucket.s3.amazonaws.com/logs/process.log");
    }

    [Fact]
    public void WithS3LogFile_WithInvalidUrl_ThrowsArgumentException()
    {
        var builder = new AiProvenanceBuilder();

        Should.Throw<ArgumentException>(() => builder.WithS3LogFile("ftp://invalid.com"));
        Should.Throw<ArgumentException>(() => builder.WithS3LogFile("invalid-url"));
    }

    [Fact]
    public void WithOccurredDateTime_SetsOccurred()
    {
        var occurredTime = new DateTimeOffset(2024, 10, 1, 10, 0, 0, TimeSpan.Zero);
        var builder = new AiProvenanceBuilder()
            .ForTarget("DocumentReference", "test")
            .WithAgent("ai-algorithm", "TestAI")
            .WithOccurredDateTime(occurredTime);

        var provenance = builder.Build();
        provenance.Occurred.ShouldNotBeNull();
        ((FhirDateTime)provenance.Occurred).ToDateTimeOffset(TimeSpan.Zero).ShouldBe(occurredTime);
    }

    [Fact]
    public void WithRecordedDateTime_SetsRecorded()
    {
        var recordedTime = new DateTimeOffset(2024, 10, 1, 10, 5, 0, TimeSpan.Zero);
        var builder = new AiProvenanceBuilder()
            .ForTarget("DocumentReference", "test")
            .WithAgent("ai-algorithm", "TestAI")
            .WithRecordedDateTime(recordedTime);

        var provenance = builder.Build();
        provenance.Recorded.ShouldBe(recordedTime);
    }

    [Fact]
    public void WithReason_AddsReasonToConcepts()
    {
        var builder = new AiProvenanceBuilder()
            .ForTarget("DocumentReference", "test")
            .WithAgent("ai-algorithm", "TestAI")
            .WithReason("AI analysis");

        var provenance = builder.Build();
        provenance.Reason.Count.ShouldBe(1);
        provenance.Reason[0].Text.ShouldBe("AI analysis");
    }

    [Fact]
    public void Build_WithDefaultRecordedTime_SetsRecordedToNow()
    {
        var beforeBuild = DateTimeOffset.Now;

        var builder = new AiProvenanceBuilder()
            .ForTarget("DocumentReference", "test")
            .WithAgent("ai-algorithm", "TestAI");

        var provenance = builder.Build();
        var afterBuild = DateTimeOffset.Now;

        provenance.Recorded.Value.ShouldBeGreaterThanOrEqualTo(beforeBuild);
        provenance.Recorded.Value.ShouldBeLessThanOrEqualTo(afterBuild);
    }

    [Fact]
    public void Build_SerializesToValidFhirJson()
    {
        var builder = new AiProvenanceBuilder()
            .WithProvenanceId("prov-123")
            .ForTarget("DocumentReference", "test-doc")
            .WithAgent("ai-algorithm", "TestAI", "1.0.0")
            .WithOrganization("ThirdOpinion")
            .WithSourceEntity("Document", "original")
            .WithS3LogFile("s3://bucket/logs/process.log")
            .WithReason("AI-assisted analysis");

        var provenance = builder.Build();
        var serializer = new FhirJsonSerializer();
        var json = serializer.SerializeToString(provenance);

        json.ShouldNotBeNull();
        json.ShouldContain("\"resourceType\":\"Provenance\"");
        json.ShouldContain("\"id\":\"prov-123\"");
        json.ShouldContain("TestAI");
        json.ShouldContain("ThirdOpinion");
    }

    [Fact]
    public void FluentInterface_AllowsMethodChaining()
    {
        var result = new AiProvenanceBuilder()
            .WithProvenanceId("test")
            .ForTarget("DocumentReference", "test")
            .WithAgent("ai", "TestAI")
            .WithOrganization("TestOrg")
            .WithSourceEntity("Document", "test")
            .WithS3LogFile("s3://test/log")
            .WithReason("test")
            .WithOccurredDateTime(DateTimeOffset.Now)
            .WithRecordedDateTime(DateTimeOffset.Now);

        result.ShouldBeOfType<AiProvenanceBuilder>();
    }
}