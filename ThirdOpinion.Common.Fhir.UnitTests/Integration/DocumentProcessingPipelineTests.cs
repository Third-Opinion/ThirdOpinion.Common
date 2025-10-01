using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using ThirdOpinion.Common.Fhir.Builders.Documents;
using ThirdOpinion.Common.Fhir.Builders.Observations;
using ThirdOpinion.Common.Fhir.Builders.Provenance;
using ThirdOpinion.Common.Fhir.Configuration;
using System.Text.Json;

namespace ThirdOpinion.Common.Fhir.UnitTests.Integration;

public class DocumentProcessingPipelineTests
{
    [Fact]
    public void CompleteDocumentProcessingPipeline_CreatesLinkedResources()
    {
        var patientRef = new ResourceReference("Patient/patient-123");
        var deviceRef = new ResourceReference("Device/ai-device-123");
        var originalDocId = "original-doc-123";
        var ocrDocId = "ocr-doc-123";
        var factDocId = "fact-doc-123";
        var observationId = "recist-obs-123";
        var provenanceId = "prov-123";

        var extractedText = "Patient shows progression of target lesions. New liver metastases identified.";
        var extractedFacts = new
        {
            recist_response = "Progressive Disease",
            target_lesions = new[]
            {
                new { location = "liver", size_mm = 45, change_percent = 25 },
                new { location = "lung", size_mm = 32, change_percent = 15 }
            },
            new_lesions = new[] { "liver metastases" },
            assessment_date = "2024-10-01"
        };

        // 1. Create OCR DocumentReference
        var ocrDoc = new OcrDocumentReferenceBuilder(AiInferenceConfiguration.CreateDefault())
            .WithInferenceId(ocrDocId)
            .WithPatient(patientRef)
            .WithOcrDevice(deviceRef)
            .WithExtractedText(extractedText)
            .WithTextractRawUrl("s3://bucket/textract/raw-output.json")
            .WithTextractSimpleUrl("s3://bucket/textract/simple-output.json")
            .WithOriginalDocument(originalDocId)
            .Build();

        // 2. Create Fact Extraction DocumentReference
        var factDoc = new FactExtractionDocumentReferenceBuilder(AiInferenceConfiguration.CreateDefault())
            .WithInferenceId(factDocId)
            .WithPatient(patientRef)
            .WithExtractionDevice(deviceRef)
            .WithFactsJson(extractedFacts)
            .WithOriginalDocument(originalDocId)
            .WithOcrDocument(ocrDocId)
            .Build();

        // 3. Create RECIST Progression Observation
        var recistObs = new RecistProgressionObservationBuilder(AiInferenceConfiguration.CreateDefault())
            .WithObservationId(observationId)
            .WithPatient(patientRef)
            .WithDevice(deviceRef)
            .WithFocus(factDocId)
            .AddComponent("Target lesion count", 2)
            .AddComponent("New lesions present", true)
            .AddComponent("Overall assessment", "Progressive Disease")
            .WithRecistResponse("Progressive Disease")
            .WithBodySite("liver")
            .Build();

        // 4. Create Provenance tracking
        var provenance = new AiProvenanceBuilder()
            .WithProvenanceId(provenanceId)
            .ForTarget($"DocumentReference/{ocrDocId}")
            .ForTarget($"DocumentReference/{factDocId}")
            .ForTarget($"Observation/{observationId}")
            .WithAgent("ai-algorithm", "ThirdOpinion AI", "2.1.0")
            .WithOrganization("ThirdOpinion", "org-to")
            .WithSourceEntity("source", $"DocumentReference/{originalDocId}")
            .WithS3LogFile("s3://bucket/logs/processing-pipeline.log")
            .WithReason("Automated document processing and analysis")
            .WithOccurredDateTime(new DateTimeOffset(2024, 10, 1, 14, 30, 0, TimeSpan.Zero))
            .Build();

        // Verify all resources are created correctly
        ocrDoc.ShouldNotBeNull();
        ocrDoc.Id.ShouldBe(ocrDocId);
        ocrDoc.Content.Count.ShouldBe(1);
        ocrDoc.RelatesTo.Count.ShouldBe(1);

        factDoc.ShouldNotBeNull();
        factDoc.Id.ShouldBe(factDocId);
        factDoc.RelatesTo.Count.ShouldBe(2);

        recistObs.ShouldNotBeNull();
        recistObs.Id.ShouldBe(observationId);
        recistObs.Component.Count.ShouldBe(3);
        recistObs.Focus.Count.ShouldBe(1);
        recistObs.Focus[0].Reference.ShouldBe($"DocumentReference/{factDocId}");

        provenance.ShouldNotBeNull();
        provenance.Id.ShouldBe(provenanceId);
        provenance.Target.Count.ShouldBe(3);
        provenance.Agent.Count.ShouldBe(2);
        provenance.Entity.Count.ShouldBe(1);

        // Verify resource relationships
        var ocrRelation = ocrDoc.RelatesTo[0];
        ocrRelation.Code.ShouldBe(DocumentReferenceRelatesToCode.Transforms);
        ocrRelation.Target.Reference.ShouldBe($"DocumentReference/{originalDocId}");

        var factRelations = factDoc.RelatesTo;
        factRelations.Any(r => r.Target.Reference == $"DocumentReference/{originalDocId}").ShouldBeTrue();
        factRelations.Any(r => r.Target.Reference == $"DocumentReference/{ocrDocId}").ShouldBeTrue();

        // Verify provenance targets include all generated resources
        var provenanceTargets = provenance.Target.Select(t => t.Reference).ToList();
        provenanceTargets.ShouldContain($"DocumentReference/{ocrDocId}");
        provenanceTargets.ShouldContain($"DocumentReference/{factDocId}");
        provenanceTargets.ShouldContain($"Observation/{observationId}");
    }

    [Fact]
    public void SerializeCompleteBundle_ProducesValidFhir()
    {
        var bundle = CreateCompleteProcessingBundle();
        var serializer = new FhirJsonSerializer();
        var json = serializer.SerializeToString(bundle);

        json.ShouldNotBeNull();
        json.ShouldContain("\"resourceType\": \"Bundle\"");
        json.ShouldContain("\"type\": \"collection\"");

        // Verify all resource types are present
        json.ShouldContain("\"resourceType\": \"DocumentReference\"");
        json.ShouldContain("\"resourceType\": \"Observation\"");
        json.ShouldContain("\"resourceType\": \"Provenance\"");

        // Verify critical data is preserved
        json.ShouldContain("Progressive Disease");
        json.ShouldContain("ThirdOpinion AI");
        json.ShouldContain("liver metastases");
    }

    [Fact]
    public void PipelineWithMultipleObservations_LinksCorrectly()
    {
        var patientRef = new ResourceReference("Patient/patient-456");
        var deviceRef = new ResourceReference("Device/ai-device-456");
        var factDocId = "fact-doc-456";

        // Create fact extraction document
        var psaFacts = new { psa_value = 15.2, psa_date = "2024-10-01", psa_units = "ng/mL" };
        var factDoc = new FactExtractionDocumentReferenceBuilder(AiInferenceConfiguration.CreateDefault())
            .WithDocumentId(factDocId)
            .WithPatient(patientRef)
            .WithDevice(deviceRef)
            .WithFactsJson(psaFacts)
            .Build();

        // Create multiple observations from the same fact document
        var psaObs = new PsaProgressionObservationBuilder(AiInferenceConfiguration.CreateDefault())
            .WithObservationId("psa-obs-456")
            .WithPatient(patientRef)
            .WithDevice(deviceRef)
            .WithFocus(factDocId)
            .WithPsaValue(15.2m, "ng/mL")
            .WithTestosteroneValue(0.5m, "ng/mL")
            .WithProgressionStatus("Biochemical Progression")
            .Build();

        var recistObs = new RecistProgressionObservationBuilder(AiInferenceConfiguration.CreateDefault())
            .WithObservationId("recist-obs-456")
            .WithPatient(patientRef)
            .WithDevice(deviceRef)
            .WithFocus(factDocId)
            .WithRecistResponse("Stable Disease")
            .Build();

        // Create provenance tracking both observations
        var provenance = new AiProvenanceBuilder()
            .WithProvenanceId("prov-456")
            .ForTarget($"DocumentReference/{factDocId}")
            .ForTarget("Observation/psa-obs-456")
            .ForTarget("Observation/recist-obs-456")
            .WithAgent("ai-algorithm", "ThirdOpinion AI", "2.1.0")
            .WithOrganization("ThirdOpinion")
            .Build();

        // Verify both observations reference the same fact document
        psaObs.Focus[0].Reference.ShouldBe($"DocumentReference/{factDocId}");
        recistObs.Focus[0].Reference.ShouldBe($"DocumentReference/{factDocId}");

        // Verify provenance tracks all resources
        provenance.Target.Count.ShouldBe(3);
        var targetRefs = provenance.Target.Select(t => t.Reference).ToList();
        targetRefs.ShouldContain($"DocumentReference/{factDocId}");
        targetRefs.ShouldContain("Observation/psa-obs-456");
        targetRefs.ShouldContain("Observation/recist-obs-456");
    }

    [Fact]
    public void PipelineWithS3StoredContent_HandlesUrlsCorrectly()
    {
        var patientRef = new ResourceReference("Patient/patient-789");
        var deviceRef = new ResourceReference("Device/ai-device-789");

        // Create OCR document with S3 URLs only
        var ocrDoc = new OcrDocumentReferenceBuilder(AiInferenceConfiguration.CreateDefault())
            .WithDocumentId("ocr-doc-789")
            .WithPatient(patientRef)
            .WithDevice(deviceRef)
            .WithExtractedTextUrl("s3://bucket/extracted/text-789.txt")
            .WithTextractRawUrl("s3://bucket/textract/raw-789.json")
            .WithTextractSimpleUrl("s3://bucket/textract/simple-789.json")
            .Build();

        // Create fact extraction with S3 URL
        var factDoc = new FactExtractionDocumentReferenceBuilder(AiInferenceConfiguration.CreateDefault())
            .WithDocumentId("fact-doc-789")
            .WithPatient(patientRef)
            .WithDevice(deviceRef)
            .WithFactsJsonUrl("s3://bucket/facts/facts-789.json")
            .RelatedToDocument("ocr-doc-789", DocumentReferenceRelatesToCode.DerivedFrom)
            .Build();

        // Verify S3 URLs are preserved correctly
        ocrDoc.Content.Count.ShouldBe(3);
        ocrDoc.Content.All(c => c.Attachment.Url.StartsWith("s3://")).ShouldBeTrue();

        factDoc.Content.Count.ShouldBe(1);
        factDoc.Content[0].Attachment.Url.ShouldBe("s3://bucket/facts/facts-789.json");
    }

    [Fact]
    public void PerformanceTest_CreateLargeBundle_CompletesInReasonableTime()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Create 100 document processing chains
        var bundles = new List<Bundle>();
        for (int i = 0; i < 100; i++)
        {
            bundles.Add(CreateCompleteProcessingBundle($"test-{i}"));
        }

        stopwatch.Stop();

        bundles.Count.ShouldBe(100);
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(5000); // Should complete in under 5 seconds
    }

    private Bundle CreateCompleteProcessingBundle(string idSuffix = "test")
    {
        var patientRef = new ResourceReference($"Patient/patient-{idSuffix}");
        var deviceRef = new ResourceReference($"Device/ai-device-{idSuffix}");

        var ocrDoc = new OcrDocumentReferenceBuilder(AiInferenceConfiguration.CreateDefault())
            .WithDocumentId($"ocr-{idSuffix}")
            .WithPatient(patientRef)
            .WithDevice(deviceRef)
            .WithExtractedText("Test extracted text content")
            .Build();

        var factDoc = new FactExtractionDocumentReferenceBuilder(AiInferenceConfiguration.CreateDefault())
            .WithDocumentId($"fact-{idSuffix}")
            .WithPatient(patientRef)
            .WithDevice(deviceRef)
            .WithFactsJson(new { test = "data" })
            .RelatedToDocument($"ocr-{idSuffix}", DocumentReferenceRelatesToCode.DerivedFrom)
            .Build();

        var observation = new RecistProgressionObservationBuilder(AiInferenceConfiguration.CreateDefault())
            .WithObservationId($"obs-{idSuffix}")
            .WithPatient(patientRef)
            .WithDevice(deviceRef)
            .WithFocus($"fact-{idSuffix}")
            .WithRecistResponse("Stable Disease")
            .Build();

        var provenance = new AiProvenanceBuilder()
            .WithProvenanceId($"prov-{idSuffix}")
            .ForTarget($"DocumentReference/ocr-{idSuffix}")
            .ForTarget($"DocumentReference/fact-{idSuffix}")
            .ForTarget($"Observation/obs-{idSuffix}")
            .WithAgent("ai-algorithm", "TestAI")
            .WithOrganization("TestOrg")
            .Build();

        return new Bundle
        {
            Id = $"bundle-{idSuffix}",
            Type = Bundle.BundleType.Collection,
            Entry = new List<Bundle.EntryComponent>
            {
                new Bundle.EntryComponent { Resource = ocrDoc },
                new Bundle.EntryComponent { Resource = factDoc },
                new Bundle.EntryComponent { Resource = observation },
                new Bundle.EntryComponent { Resource = provenance }
            }
        };
    }
}