using System.Text;
using System.Text.Json;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using ThirdOpinion.Common.Fhir.Builders.Documents;
using ThirdOpinion.Common.Fhir.Configuration;
using ThirdOpinion.Common.Fhir.Helpers;

namespace ThirdOpinion.Common.Fhir.UnitTests.Builders.Documents;

public class FactExtractionDocumentReferenceBuilderTests
{
    private readonly AiInferenceConfiguration _configuration;
    private readonly ResourceReference _deviceReference;
    private readonly ResourceReference _ocrDocumentReference;
    private readonly ResourceReference _originalDocumentReference;
    private readonly ResourceReference _patientReference;

    public FactExtractionDocumentReferenceBuilderTests()
    {
        _configuration = AiInferenceConfiguration.CreateDefault();
        _patientReference = new ResourceReference("Patient/test-patient", "Test Patient");
        _deviceReference
            = new ResourceReference("Device/extraction-device", "Fact Extraction Device");
        _originalDocumentReference
            = new ResourceReference("DocumentReference/original-doc", "Original Document");
        _ocrDocumentReference = new ResourceReference("DocumentReference/ocr-doc", "OCR Document");
    }

    [Fact]
    public void Build_WithObjectSerialization_CreatesCorrectDocumentReference()
    {
        // Arrange
        var builder = new FactExtractionDocumentReferenceBuilder(_configuration);
        var facts = new
        {
            PatientName = "John Doe",
            Diagnosis = "Hypertension",
            Medications = new[] { "Lisinopril", "Hydrochlorothiazide" },
            VitalSigns = new { BloodPressure = "140/90", HeartRate = 72 }
        };

        // Act
        DocumentReference document = builder
            .WithPatient(_patientReference)
            .WithExtractionDevice(_deviceReference)
            .WithOriginalDocument(_originalDocumentReference)
            .WithOcrDocument(_ocrDocumentReference)
            .WithFactsJson(facts, "Extracted Medical Facts")
            .Build();

        // Assert
        document.ShouldNotBeNull();
        document.Status.ShouldBe(DocumentReferenceStatus.Current);

        // Check type
        document.Type.Coding[0].System.ShouldBe(FhirCodingHelper.Systems.LOINC_SYSTEM);
        document.Type.Coding[0].Code.ShouldBe("11506-3");
        document.Type.Text.ShouldBe("Fact Extraction Results Document");

        // Check subject
        document.Subject.ShouldBe(_patientReference);

        // Check author
        document.Author.ShouldNotBeNull();
        document.Author.Count.ShouldBe(1);
        document.Author[0].ShouldBe(_deviceReference);

        // Check relatesTo (should have both original and OCR documents)
        document.RelatesTo.ShouldNotBeNull();
        document.RelatesTo.Count.ShouldBe(2);
        document.RelatesTo.ShouldContain(r => r.Target == _originalDocumentReference);
        document.RelatesTo.ShouldContain(r => r.Target == _ocrDocumentReference);
        document.RelatesTo.All(r => r.Code == DocumentRelationshipType.Transforms).ShouldBeTrue();

        // Check content
        document.Content.ShouldNotBeNull();
        document.Content.Count.ShouldBe(1);
        DocumentReference.ContentComponent? content = document.Content[0];
        content.Attachment.ContentType.ShouldBe("application/json");
        content.Attachment.Title.ShouldBe("Extracted Medical Facts");
        content.Attachment.Data.ShouldNotBeNull();

        // Verify JSON content
        string jsonString = Encoding.UTF8.GetString(content.Attachment.Data);
        var deserializedFacts = JsonSerializer.Deserialize<JsonElement>(jsonString);
        deserializedFacts.GetProperty("PatientName").GetString().ShouldBe("John Doe");
        deserializedFacts.GetProperty("Diagnosis").GetString().ShouldBe("Hypertension");
    }

    [Fact]
    public void Build_WithJsonString_CreatesCorrectDocumentReference()
    {
        // Arrange
        var builder = new FactExtractionDocumentReferenceBuilder(_configuration);
        var jsonString = @"{
            ""findings"": [
                {""type"": ""condition"", ""value"": ""diabetes""},
                {""type"": ""medication"", ""value"": ""metformin""}
            ],
            ""confidence"": 0.95
        }";

        // Act
        DocumentReference document = builder
            .WithPatient(_patientReference)
            .WithExtractionDevice(_deviceReference)
            .WithFactsJson(jsonString)
            .Build();

        // Assert
        document.Content.Count.ShouldBe(1);
        DocumentReference.ContentComponent? content = document.Content[0];
        content.Attachment.ContentType.ShouldBe("application/json");
        content.Attachment.Title.ShouldBe("Extracted Facts");

        // Verify JSON content is preserved
        string storedJson = Encoding.UTF8.GetString(content.Attachment.Data);
        var parsedJson = JsonSerializer.Deserialize<JsonElement>(storedJson);
        parsedJson.GetProperty("confidence").GetDouble().ShouldBe(0.95);
    }

    [Fact]
    public void Build_WithS3Url_CreatesCorrectDocumentReference()
    {
        // Arrange
        var builder = new FactExtractionDocumentReferenceBuilder(_configuration);
        var s3Url = "https://bucket.s3.amazonaws.com/extracted-facts.json";

        // Act
        DocumentReference document = builder
            .WithPatient(_patientReference)
            .WithExtractionDevice(_deviceReference)
            .WithFactsJsonUrl(s3Url, "Large Facts Dataset")
            .Build();

        // Assert
        document.Content.Count.ShouldBe(1);
        DocumentReference.ContentComponent? content = document.Content[0];
        content.Attachment.ContentType.ShouldBe("application/json");
        content.Attachment.Url.ShouldBe(s3Url);
        content.Attachment.Title.ShouldBe("Large Facts Dataset");
        content.Attachment.Data.ShouldBeNull();
    }

    [Fact]
    public void Build_WithOnlyOriginalDocument_CreatesCorrectRelatesTo()
    {
        // Arrange
        var builder = new FactExtractionDocumentReferenceBuilder(_configuration);

        // Act
        DocumentReference document = builder
            .WithPatient(_patientReference)
            .WithExtractionDevice(_deviceReference)
            .WithOriginalDocument(_originalDocumentReference)
            .WithFactsJson("{\"test\": true}")
            .Build();

        // Assert
        document.RelatesTo.Count.ShouldBe(1);
        document.RelatesTo[0].Target.ShouldBe(_originalDocumentReference);
    }

    [Fact]
    public void Build_WithOnlyOcrDocument_CreatesCorrectRelatesTo()
    {
        // Arrange
        var builder = new FactExtractionDocumentReferenceBuilder(_configuration);

        // Act
        DocumentReference document = builder
            .WithPatient(_patientReference)
            .WithExtractionDevice(_deviceReference)
            .WithOcrDocument(_ocrDocumentReference)
            .WithFactsJson("{\"test\": true}")
            .Build();

        // Assert
        document.RelatesTo.Count.ShouldBe(1);
        document.RelatesTo[0].Target.ShouldBe(_ocrDocumentReference);
    }

    [Fact]
    public void WithFactsJson_AfterUrl_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new FactExtractionDocumentReferenceBuilder(_configuration);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
            builder
                .WithPatient(_patientReference)
                .WithExtractionDevice(_deviceReference)
                .WithFactsJsonUrl("https://example.com/facts.json")
                .WithFactsJson("{\"test\": true}"));

        exception.Message.ShouldContain(
            "Cannot add inline content when URL content has already been set");
    }

    [Fact]
    public void WithFactsJsonUrl_AfterInlineJson_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new FactExtractionDocumentReferenceBuilder(_configuration);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
            builder
                .WithPatient(_patientReference)
                .WithExtractionDevice(_deviceReference)
                .WithFactsJson("{\"test\": true}")
                .WithFactsJsonUrl("https://example.com/facts.json"));

        exception.Message.ShouldContain(
            "Cannot add URL content when inline content has already been set");
    }

    [Fact]
    public void WithFactsJson_InvalidJson_ThrowsArgumentException()
    {
        // Arrange
        var builder = new FactExtractionDocumentReferenceBuilder(_configuration);
        var invalidJson = "{ invalid json }";

        // Act & Assert
        var exception = Should.Throw<ArgumentException>(() =>
            builder.WithFactsJson(invalidJson));

        exception.Message.ShouldContain("Invalid JSON format");
    }

    [Fact]
    public void WithFactsJson_ObjectSerializationFailure_ThrowsArgumentException()
    {
        // Arrange
        var builder = new FactExtractionDocumentReferenceBuilder(_configuration);
        var cyclicObject = new TestObjectWithCycle();
        cyclicObject.Self = cyclicObject; // Create a cycle that can't be serialized

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            builder.WithFactsJson(cyclicObject));
    }

    [Fact]
    public void Build_WithoutPatient_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new FactExtractionDocumentReferenceBuilder(_configuration);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
            builder
                .WithExtractionDevice(_deviceReference)
                .WithFactsJson("{\"test\": true}")
                .Build());

        exception.Message.ShouldContain("Patient reference is required");
    }

    [Fact]
    public void Build_WithoutDevice_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new FactExtractionDocumentReferenceBuilder(_configuration);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
            builder
                .WithPatient(_patientReference)
                .WithFactsJson("{\"test\": true}")
                .Build());

        exception.Message.ShouldContain("Extraction device reference is required");
    }

    [Fact]
    public void Build_WithoutContent_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new FactExtractionDocumentReferenceBuilder(_configuration);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
            builder
                .WithPatient(_patientReference)
                .WithExtractionDevice(_deviceReference)
                .Build());

        exception.Message.ShouldContain("At least one facts content attachment is required");
    }

    [Fact]
    public void WithFactsJson_NullObject_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new FactExtractionDocumentReferenceBuilder(_configuration);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            builder.WithFactsJson((object)null!));
    }

    [Fact]
    public void WithFactsJson_EmptyString_ThrowsArgumentException()
    {
        // Arrange
        var builder = new FactExtractionDocumentReferenceBuilder(_configuration);

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            builder.WithFactsJson(""));
    }

    [Fact]
    public void WithFactsJsonUrl_EmptyUrl_ThrowsArgumentException()
    {
        // Arrange
        var builder = new FactExtractionDocumentReferenceBuilder(_configuration);

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            builder.WithFactsJsonUrl(""));
    }

    [Fact]
    public void FluentInterface_SupportsCompleteChaining()
    {
        // Arrange & Act
        var facts = new { condition = "diabetes", medication = "insulin" };
        DocumentReference document = new FactExtractionDocumentReferenceBuilder(_configuration)
            .WithFhirResourceId("fact-001")
            .WithPatient("Patient/p123", "Jane Smith")
            .WithExtractionDevice("Device/d456", "AI Fact Extractor")
            .WithOriginalDocument("DocumentReference/orig789", "Original Report")
            .WithOcrDocument("DocumentReference/ocr456", "OCR Text")
            .WithFactsJson(facts, "Medical Facts")
            .AddDerivedFrom("Procedure/extraction123", "Fact Extraction Process")
            .Build();

        // Assert
        document.Id.ShouldBe("to.ai-fact-001");
        document.Subject.Reference.ShouldBe("Patient/p123");
        document.Author[0].Reference.ShouldBe("Device/d456");
        document.RelatesTo.Count.ShouldBe(2);
        document.Content.Count.ShouldBe(1);
        document.Content[0].Attachment.Title.ShouldBe("Medical Facts");
    }

    [Fact]
    public void Build_GeneratesValidFhirJson()
    {
        // Arrange
        var builder = new FactExtractionDocumentReferenceBuilder(_configuration);
        var facts = new
        {
            extractedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            facts = new[]
            {
                new { type = "diagnosis", value = "Type 2 Diabetes", confidence = 0.95 },
                new { type = "medication", value = "Metformin 500mg", confidence = 0.88 }
            }
        };

        DocumentReference document = builder
            .WithPatient(_patientReference)
            .WithExtractionDevice(_deviceReference)
            .WithOriginalDocument(_originalDocumentReference)
            .WithOcrDocument(_ocrDocumentReference)
            .WithFactsJson(facts)
            .Build();

        // Act
        var serializer = new FhirJsonSerializer(new SerializerSettings { Pretty = true });
        string json = serializer.SerializeToString(document);

        // Assert
        json.ShouldNotBeNullOrEmpty();
        json.ShouldContain("\"resourceType\": \"DocumentReference\"");
        json.ShouldContain("\"status\": \"current\"");
        json.ShouldContain("11506-3"); // LOINC code
        json.ShouldContain("\"contentType\": \"application/json\"");
        json.ShouldContain("\"code\": \"transforms\""); // relatesTo code
        json.ShouldContain("\"code\": \"AIAST\""); // AIAST security label

        // Verify it can be deserialized
        var parser = new FhirJsonParser();
        var deserializedDoc = parser.Parse<DocumentReference>(json);
        deserializedDoc.ShouldNotBeNull();
        deserializedDoc.Status.ShouldBe(DocumentReferenceStatus.Current);
        deserializedDoc.Content.Count.ShouldBe(1);
        deserializedDoc.RelatesTo.Count.ShouldBe(2);
    }

    [Fact]
    public void WithFactsJsonUrl_DefaultTitle_SetsDefaultTitle()
    {
        // Arrange
        var builder = new FactExtractionDocumentReferenceBuilder(_configuration);

        // Act
        DocumentReference document = builder
            .WithPatient(_patientReference)
            .WithExtractionDevice(_deviceReference)
            .WithFactsJsonUrl("https://example.com/facts.json")
            .Build();

        // Assert
        document.Content[0].Attachment.Title.ShouldBe("Extracted Facts");
    }

    [Fact]
    public void IntegrationTest_DocumentProcessingPipeline()
    {
        // Arrange - Simulate a complete document processing pipeline
        var originalDocRef
            = new ResourceReference("DocumentReference/original-123", "Original Medical Report");
        var ocrDocRef = new ResourceReference("DocumentReference/ocr-456", "OCR Extracted Text");
        var patientRef = new ResourceReference("Patient/patient-789", "John Doe");
        var ocrDeviceRef = new ResourceReference("Device/ocr-device-001", "AWS Textract");
        var extractionDeviceRef
            = new ResourceReference("Device/extraction-device-002", "Medical Fact Extractor");

        // Step 1: Create OCR document
        DocumentReference ocrDocument = new OcrDocumentReferenceBuilder(_configuration)
            .WithPatient(patientRef)
            .WithOcrDevice(ocrDeviceRef)
            .WithOriginalDocument(originalDocRef)
            .WithExtractedText(
                "Patient John Doe presents with Type 2 Diabetes. Current medication: Metformin 500mg twice daily.")
            .Build();

        // Step 2: Create fact extraction document
        var extractedFacts = new
        {
            patient = new { name = "John Doe", id = "patient-789" },
            conditions
                = new[] { new { name = "Type 2 Diabetes", code = "E11", system = "ICD-10" } },
            medications = new[]
                { new { name = "Metformin", dose = "500mg", frequency = "twice daily" } },
            extractionMetadata = new { confidence = 0.92, model = "medical-nlp-v2.1" }
        };

        DocumentReference factDocument = new FactExtractionDocumentReferenceBuilder(_configuration)
            .WithPatient(patientRef)
            .WithExtractionDevice(extractionDeviceRef)
            .WithOriginalDocument(originalDocRef)
            .WithOcrDocument(ocrDocRef)
            .WithFactsJson(extractedFacts, "Structured Medical Facts")
            .Build();

        // Assert - Verify the complete pipeline
        // OCR document should relate to original
        ocrDocument.RelatesTo.Count.ShouldBe(1);
        ocrDocument.RelatesTo[0].Target.ShouldBe(originalDocRef);
        ocrDocument.RelatesTo[0].Code.ShouldBe(DocumentRelationshipType.Transforms);

        // Fact document should relate to both original and OCR
        factDocument.RelatesTo.Count.ShouldBe(2);
        factDocument.RelatesTo.ShouldContain(r => r.Target == originalDocRef);
        factDocument.RelatesTo.ShouldContain(r => r.Target == ocrDocRef);

        // Both documents should have the same patient
        ocrDocument.Subject.ShouldBe(patientRef);
        factDocument.Subject.ShouldBe(patientRef);

        // Verify content types
        ocrDocument.Content[0].Attachment.ContentType.ShouldBe("text/plain");
        factDocument.Content[0].Attachment.ContentType.ShouldBe("application/json");

        // Verify extracted facts content
        string factsJson = Encoding.UTF8.GetString(factDocument.Content[0].Attachment.Data);
        var parsedFacts = JsonSerializer.Deserialize<JsonElement>(factsJson);
        parsedFacts.GetProperty("patient").GetProperty("name").GetString().ShouldBe("John Doe");
        parsedFacts.GetProperty("extractionMetadata").GetProperty("confidence").GetDouble()
            .ShouldBe(0.92);
    }

    private class TestObjectWithCycle
    {
        public TestObjectWithCycle? Self { get; set; }
    }
}