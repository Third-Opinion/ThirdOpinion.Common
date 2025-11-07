using System.Text;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using ThirdOpinion.Common.Fhir.Builders.Documents;
using ThirdOpinion.Common.Fhir.Configuration;
using ThirdOpinion.Common.Fhir.Helpers;

namespace ThirdOpinion.Common.Fhir.UnitTests.Builders.Documents;

public class OcrDocumentReferenceBuilderTests
{
    private readonly AiInferenceConfiguration _configuration;
    private readonly ResourceReference _deviceReference;
    private readonly ResourceReference _originalDocumentReference;
    private readonly ResourceReference _patientReference;

    public OcrDocumentReferenceBuilderTests()
    {
        _configuration = AiInferenceConfiguration.CreateDefault();
        _patientReference = new ResourceReference("Patient/test-patient", "Test Patient");
        _deviceReference = new ResourceReference("Device/ocr-device", "OCR Processing Device");
        _originalDocumentReference
            = new ResourceReference("DocumentReference/original-doc", "Original Document");
    }

    [Fact]
    public void Build_WithInlineText_CreatesCorrectDocumentReference()
    {
        // Arrange
        var builder = new OcrDocumentReferenceBuilder(_configuration);
        var extractedText = "This is the extracted text from the document.";

        // Act
        DocumentReference document = builder
            .WithPatient(_patientReference)
            .WithOcrDevice(_deviceReference)
            .WithOriginalDocument(_originalDocumentReference)
            .WithExtractedText(extractedText, "Extracted Text Title")
            .Build();

        // Assert
        document.ShouldNotBeNull();
        document.Status.ShouldBe(DocumentReferenceStatus.Current);

        // Check type
        document.Type.Coding[0].System.ShouldBe(FhirCodingHelper.Systems.LOINC_SYSTEM);
        document.Type.Coding[0].Code.ShouldBe("18842-5");
        document.Type.Text.ShouldBe("OCR Extracted Text Document");

        // Check subject
        document.Subject.ShouldBe(_patientReference);

        // Check author
        document.Author.ShouldNotBeNull();
        document.Author.Count.ShouldBe(1);
        document.Author[0].ShouldBe(_deviceReference);

        // Check relatesTo
        document.RelatesTo.ShouldNotBeNull();
        document.RelatesTo.Count.ShouldBe(1);
        document.RelatesTo[0].Code.ShouldBe(DocumentRelationshipType.Transforms);
        document.RelatesTo[0].Target.ShouldBe(_originalDocumentReference);

        // Check content
        document.Content.ShouldNotBeNull();
        document.Content.Count.ShouldBe(1);
        DocumentReference.ContentComponent? content = document.Content[0];
        content.Attachment.ContentType.ShouldBe("text/plain");
        content.Attachment.Title.ShouldBe("Extracted Text Title");
        content.Attachment.Data.ShouldNotBeNull();

        // Verify Base64 encoding
        string decodedText = Encoding.UTF8.GetString(content.Attachment.Data);
        decodedText.ShouldBe(extractedText);
    }

    [Fact]
    public void Build_WithS3Url_CreatesCorrectDocumentReference()
    {
        // Arrange
        var builder = new OcrDocumentReferenceBuilder(_configuration);
        var s3Url = "https://bucket.s3.amazonaws.com/extracted-text.txt";

        // Act
        DocumentReference document = builder
            .WithPatient(_patientReference)
            .WithOcrDevice(_deviceReference)
            .WithOriginalDocument(_originalDocumentReference)
            .WithExtractedTextUrl(s3Url)
            .Build();

        // Assert
        document.Content.Count.ShouldBe(1);
        DocumentReference.ContentComponent? content = document.Content[0];
        content.Attachment.ContentType.ShouldBe("text/plain");
        content.Attachment.Url.ShouldBe(s3Url);
        content.Attachment.Data.ShouldBeNull();
    }

    [Fact]
    public void Build_WithTextractRawUrl_CreatesCorrectDocumentReference()
    {
        // Arrange
        var builder = new OcrDocumentReferenceBuilder(_configuration);
        var textractUrl = "https://bucket.s3.amazonaws.com/textract-raw-output.json";

        // Act
        DocumentReference document = builder
            .WithPatient(_patientReference)
            .WithOcrDevice(_deviceReference)
            .WithOriginalDocument(_originalDocumentReference)
            .WithTextractRawUrl(textractUrl, "Textract Raw")
            .Build();

        // Assert
        document.Content.Count.ShouldBe(1);
        DocumentReference.ContentComponent? content = document.Content[0];
        content.Attachment.ContentType.ShouldBe("application/json");
        content.Attachment.Url.ShouldBe(textractUrl);
        content.Attachment.Title.ShouldBe("Textract Raw");
    }

    [Fact]
    public void Build_WithTextractSimpleUrl_CreatesCorrectDocumentReference()
    {
        // Arrange
        var builder = new OcrDocumentReferenceBuilder(_configuration);
        var textractUrl = "https://bucket.s3.amazonaws.com/textract-simple-output.json";

        // Act
        DocumentReference document = builder
            .WithPatient(_patientReference)
            .WithOcrDevice(_deviceReference)
            .WithOriginalDocument(_originalDocumentReference)
            .WithTextractSimpleUrl(textractUrl)
            .Build();

        // Assert
        document.Content.Count.ShouldBe(1);
        DocumentReference.ContentComponent? content = document.Content[0];
        content.Attachment.ContentType.ShouldBe("application/json");
        content.Attachment.Url.ShouldBe(textractUrl);
        content.Attachment.Title.ShouldBe("Textract Simplified Output");
    }

    [Fact]
    public void WithExtractedText_AfterUrl_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new OcrDocumentReferenceBuilder(_configuration);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
            builder
                .WithPatient(_patientReference)
                .WithOcrDevice(_deviceReference)
                .WithOriginalDocument(_originalDocumentReference)
                .WithExtractedTextUrl("https://example.com/text.txt")
                .WithExtractedText("Some text"));

        exception.Message.ShouldContain(
            "Cannot add inline content when URL content has already been set");
    }

    [Fact]
    public void WithExtractedTextUrl_AfterInlineText_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new OcrDocumentReferenceBuilder(_configuration);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
            builder
                .WithPatient(_patientReference)
                .WithOcrDevice(_deviceReference)
                .WithOriginalDocument(_originalDocumentReference)
                .WithExtractedText("Some text")
                .WithExtractedTextUrl("https://example.com/text.txt"));

        exception.Message.ShouldContain(
            "Cannot add URL content when inline content has already been set");
    }

    [Fact]
    public void Build_WithoutPatient_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new OcrDocumentReferenceBuilder(_configuration);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
            builder
                .WithOcrDevice(_deviceReference)
                .WithOriginalDocument(_originalDocumentReference)
                .WithExtractedText("Some text")
                .Build());

        exception.Message.ShouldContain("Patient reference is required");
    }

    [Fact]
    public void Build_WithoutDevice_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new OcrDocumentReferenceBuilder(_configuration);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
            builder
                .WithPatient(_patientReference)
                .WithOriginalDocument(_originalDocumentReference)
                .WithExtractedText("Some text")
                .Build());

        exception.Message.ShouldContain("OCR device reference is required");
    }

    [Fact]
    public void Build_WithoutOriginalDocument_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new OcrDocumentReferenceBuilder(_configuration);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
            builder
                .WithPatient(_patientReference)
                .WithOcrDevice(_deviceReference)
                .WithExtractedText("Some text")
                .Build());

        exception.Message.ShouldContain("Original document reference is required");
    }

    [Fact]
    public void Build_WithoutContent_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new OcrDocumentReferenceBuilder(_configuration);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
            builder
                .WithPatient(_patientReference)
                .WithOcrDevice(_deviceReference)
                .WithOriginalDocument(_originalDocumentReference)
                .Build());

        exception.Message.ShouldContain("At least one content attachment is required");
    }

    [Fact]
    public void WithPatient_NullPatient_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new OcrDocumentReferenceBuilder(_configuration);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            builder.WithPatient(null!));
    }

    [Fact]
    public void WithPatient_EmptyPatientId_ThrowsArgumentException()
    {
        // Arrange
        var builder = new OcrDocumentReferenceBuilder(_configuration);

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            builder.WithPatient("", "Display"));
    }

    [Fact]
    public void WithOcrDevice_NullDevice_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new OcrDocumentReferenceBuilder(_configuration);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            builder.WithOcrDevice(null!));
    }

    [Fact]
    public void WithExtractedText_EmptyText_ThrowsArgumentException()
    {
        // Arrange
        var builder = new OcrDocumentReferenceBuilder(_configuration);

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            builder.WithExtractedText(""));
    }

    [Fact]
    public void WithExtractedTextUrl_EmptyUrl_ThrowsArgumentException()
    {
        // Arrange
        var builder = new OcrDocumentReferenceBuilder(_configuration);

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            builder.WithExtractedTextUrl(""));
    }

    [Fact]
    public void FluentInterface_SupportsCompleteChaining()
    {
        // Arrange & Act
        DocumentReference document = new OcrDocumentReferenceBuilder(_configuration)
            .WithInferenceId("ocr-001")
            .WithPatient("Patient/p123", "John Doe")
            .WithOcrDevice("Device/d456", "OCR AI Device")
            .WithOriginalDocument("DocumentReference/doc789", "Original PDF")
            .WithExtractedText("Extracted text content", "OCR Results")
            .AddDerivedFrom("Procedure/proc123", "OCR Process")
            .Build();

        // Assert
        document.Id.ShouldBe("ocr-001");
        document.Subject.Reference.ShouldBe("Patient/p123");
        document.Author[0].Reference.ShouldBe("Device/d456");
        document.RelatesTo[0].Target.Reference.ShouldBe("DocumentReference/doc789");
        document.Content.Count.ShouldBe(1);
        document.Content[0].Attachment.Title.ShouldBe("OCR Results");
    }

    [Fact]
    public void Build_GeneratesValidFhirJson()
    {
        // Arrange
        var builder = new OcrDocumentReferenceBuilder(_configuration);
        DocumentReference document = builder
            .WithPatient(_patientReference)
            .WithOcrDevice(_deviceReference)
            .WithOriginalDocument(_originalDocumentReference)
            .WithExtractedText("Sample extracted text from OCR processing")
            .Build();

        // Act
        var serializer = new FhirJsonSerializer(new SerializerSettings { Pretty = true });
        string json = serializer.SerializeToString(document);

        // Assert
        json.ShouldNotBeNullOrEmpty();
        json.ShouldContain("\"resourceType\": \"DocumentReference\"");
        json.ShouldContain("\"status\": \"current\"");
        json.ShouldContain("18842-5"); // LOINC code
        json.ShouldContain("\"contentType\": \"text/plain\"");
        json.ShouldContain("\"code\": \"transforms\""); // relatesTo code
        json.ShouldContain("\"code\": \"AIAST\""); // AIAST security label

        // Verify it can be deserialized
        var parser = new FhirJsonParser();
        var deserializedDoc = parser.Parse<DocumentReference>(json);
        deserializedDoc.ShouldNotBeNull();
        deserializedDoc.Status.ShouldBe(DocumentReferenceStatus.Current);
        deserializedDoc.Content.Count.ShouldBe(1);
        deserializedDoc.RelatesTo.Count.ShouldBe(1);
    }

    [Fact]
    public void WithPatient_StringId_AddsPatientPrefix()
    {
        // Arrange
        var builder = new OcrDocumentReferenceBuilder(_configuration);

        // Act
        DocumentReference document = builder
            .WithPatient("123", "Test Patient")
            .WithOcrDevice(_deviceReference)
            .WithOriginalDocument(_originalDocumentReference)
            .WithExtractedText("Text")
            .Build();

        // Assert
        document.Subject.Reference.ShouldBe("Patient/123");
        document.Subject.Display.ShouldBe("Test Patient");
    }

    [Fact]
    public void WithOcrDevice_StringId_AddsDevicePrefix()
    {
        // Arrange
        var builder = new OcrDocumentReferenceBuilder(_configuration);

        // Act
        DocumentReference document = builder
            .WithPatient(_patientReference)
            .WithOcrDevice("456", "OCR Device")
            .WithOriginalDocument(_originalDocumentReference)
            .WithExtractedText("Text")
            .Build();

        // Assert
        document.Author[0].Reference.ShouldBe("Device/456");
        document.Author[0].Display.ShouldBe("OCR Device");
    }

    [Fact]
    public void WithOriginalDocument_StringId_AddsDocumentReferencePrefix()
    {
        // Arrange
        var builder = new OcrDocumentReferenceBuilder(_configuration);

        // Act
        DocumentReference document = builder
            .WithPatient(_patientReference)
            .WithOcrDevice(_deviceReference)
            .WithOriginalDocument("789", "Original Doc")
            .WithExtractedText("Text")
            .Build();

        // Assert
        document.RelatesTo[0].Target.Reference.ShouldBe("DocumentReference/789");
        document.RelatesTo[0].Target.Display.ShouldBe("Original Doc");
    }

    [Fact]
    public void WithExtractedText_DefaultTitle_SetsDefaultTitle()
    {
        // Arrange
        var builder = new OcrDocumentReferenceBuilder(_configuration);

        // Act
        DocumentReference document = builder
            .WithPatient(_patientReference)
            .WithOcrDevice(_deviceReference)
            .WithOriginalDocument(_originalDocumentReference)
            .WithExtractedText("Text content")
            .Build();

        // Assert
        document.Content[0].Attachment.Title.ShouldBe("OCR Extracted Text");
    }
}