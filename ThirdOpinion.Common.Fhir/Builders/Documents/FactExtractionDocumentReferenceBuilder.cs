using System.Text;
using System.Text.Json;
using Hl7.Fhir.Model;
using ThirdOpinion.Common.Fhir.Builders.Base;
using ThirdOpinion.Common.Fhir.Configuration;
using ThirdOpinion.Common.Fhir.Helpers;

namespace ThirdOpinion.Common.Fhir.Builders.Documents;

/// <summary>
///     Builder for creating FHIR DocumentReference resources for fact extraction results
/// </summary>
public class FactExtractionDocumentReferenceBuilder : AiResourceBuilderBase<DocumentReference, FactExtractionDocumentReferenceBuilder>
{
    private readonly List<DocumentReference.ContentComponent> _contents;
    private bool _hasInlineContent;
    private bool _hasUrlContent;
    private ResourceReference? _ocrDocumentReference;
    private ResourceReference? _originalDocumentReference;

    /// <summary>
    ///     Creates a new Fact Extraction DocumentReference builder
    /// </summary>
    /// <param name="configuration">The AI inference configuration</param>
    public FactExtractionDocumentReferenceBuilder(AiInferenceConfiguration configuration)
        : base(configuration)
    {
        _contents = new List<DocumentReference.ContentComponent>();
        _hasInlineContent = false;
        _hasUrlContent = false;
    }

    /// <summary>
    ///     Sets the fact extraction device reference that processed the document
    /// </summary>
    /// <param name="device">The extraction device resource reference</param>
    /// <returns>This builder instance for method chaining</returns>
    public FactExtractionDocumentReferenceBuilder WithExtractionDevice(ResourceReference device)
    {
        return WithDevice(device);
    }

    /// <summary>
    ///     Sets the fact extraction device reference that processed the document
    /// </summary>
    /// <param name="deviceId">The extraction device ID</param>
    /// <param name="display">Optional display text</param>
    /// <returns>This builder instance for method chaining</returns>
    public FactExtractionDocumentReferenceBuilder WithExtractionDevice(string deviceId,
        string? display = null)
    {
        return WithDevice(deviceId, display);
    }

    /// <summary>
    ///     Sets the original document that this fact extraction document relates to
    /// </summary>
    /// <param name="originalDocument">The original document reference</param>
    /// <returns>This builder instance for method chaining</returns>
    public FactExtractionDocumentReferenceBuilder WithOriginalDocument(
        ResourceReference originalDocument)
    {
        _originalDocumentReference = originalDocument ??
                                     throw new ArgumentNullException(nameof(originalDocument));
        return this;
    }

    /// <summary>
    ///     Sets the original document that this fact extraction document relates to
    /// </summary>
    /// <param name="documentId">The original document ID</param>
    /// <param name="display">Optional display text</param>
    /// <returns>This builder instance for method chaining</returns>
    public FactExtractionDocumentReferenceBuilder WithOriginalDocument(string documentId,
        string? display = null)
    {
        if (string.IsNullOrWhiteSpace(documentId))
            throw new ArgumentException("Document ID cannot be null or empty", nameof(documentId));

        _originalDocumentReference = new ResourceReference
        {
            Reference = documentId.StartsWith("DocumentReference/")
                ? documentId
                : $"DocumentReference/{documentId}",
            Display = display
        };
        return this;
    }

    /// <summary>
    ///     Sets the OCR document that this fact extraction document relates to
    /// </summary>
    /// <param name="ocrDocument">The OCR document reference</param>
    /// <returns>This builder instance for method chaining</returns>
    public FactExtractionDocumentReferenceBuilder WithOcrDocument(ResourceReference ocrDocument)
    {
        _ocrDocumentReference = ocrDocument ?? throw new ArgumentNullException(nameof(ocrDocument));
        return this;
    }

    /// <summary>
    ///     Sets the OCR document that this fact extraction document relates to
    /// </summary>
    /// <param name="documentId">The OCR document ID</param>
    /// <param name="display">Optional display text</param>
    /// <returns>This builder instance for method chaining</returns>
    public FactExtractionDocumentReferenceBuilder WithOcrDocument(string documentId,
        string? display = null)
    {
        if (string.IsNullOrWhiteSpace(documentId))
            throw new ArgumentException("Document ID cannot be null or empty", nameof(documentId));

        _ocrDocumentReference = new ResourceReference
        {
            Reference = documentId.StartsWith("DocumentReference/")
                ? documentId
                : $"DocumentReference/{documentId}",
            Display = display
        };
        return this;
    }

    /// <summary>
    ///     Adds extracted facts as JSON content from an object with automatic serialization
    /// </summary>
    /// <param name="facts">The facts object to serialize and store</param>
    /// <param name="title">Optional title for the content</param>
    /// <returns>This builder instance for method chaining</returns>
    public FactExtractionDocumentReferenceBuilder WithFactsJson(object facts, string? title = null)
    {
        if (facts == null)
            throw new ArgumentNullException(nameof(facts));

        if (_hasUrlContent)
            throw new InvalidOperationException(
                "Cannot add inline content when URL content has already been set. Use either inline OR URL content, not both.");

        try
        {
            string jsonString = JsonSerializer.Serialize(facts, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            return WithFactsJson(jsonString, title);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("Failed to serialize facts object to JSON", nameof(facts),
                ex);
        }
    }

    /// <summary>
    ///     Adds extracted facts as JSON content from a pre-serialized JSON string
    /// </summary>
    /// <param name="jsonString">The JSON string containing extracted facts</param>
    /// <param name="title">Optional title for the content</param>
    /// <returns>This builder instance for method chaining</returns>
    public FactExtractionDocumentReferenceBuilder WithFactsJson(string jsonString,
        string? title = null)
    {
        if (string.IsNullOrEmpty(jsonString))
            throw new ArgumentException("JSON string cannot be null or empty", nameof(jsonString));

        if (_hasUrlContent)
            throw new InvalidOperationException(
                "Cannot add inline content when URL content has already been set. Use either inline OR URL content, not both.");

        // Validate JSON format
        try
        {
            JsonDocument.Parse(jsonString);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("Invalid JSON format", nameof(jsonString), ex);
        }

        byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonString);

        var content = new DocumentReference.ContentComponent
        {
            Attachment = new Attachment
            {
                ContentType = "application/json",
                Data = jsonBytes,
                Title = title ?? "Extracted Facts"
            }
        };

        _contents.Add(content);
        _hasInlineContent = true;
        return this;
    }

    /// <summary>
    ///     Adds extracted facts via S3 URL reference
    /// </summary>
    /// <param name="s3Url">The S3 URL to the facts JSON file</param>
    /// <param name="title">Optional title for the content</param>
    /// <returns>This builder instance for method chaining</returns>
    public FactExtractionDocumentReferenceBuilder WithFactsJsonUrl(string s3Url,
        string? title = null)
    {
        if (string.IsNullOrWhiteSpace(s3Url))
            throw new ArgumentException("S3 URL cannot be null or empty", nameof(s3Url));

        if (_hasInlineContent)
            throw new InvalidOperationException(
                "Cannot add URL content when inline content has already been set. Use either inline OR URL content, not both.");

        var content = new DocumentReference.ContentComponent
        {
            Attachment = new Attachment
            {
                ContentType = "application/json",
                Url = s3Url,
                Title = title ?? "Extracted Facts"
            }
        };

        _contents.Add(content);
        _hasUrlContent = true;
        return this;
    }

    /// <summary>
    ///     Validates that required fields are set before building
    /// </summary>
    protected override void ValidateRequiredFields()
    {
        if (PatientReference == null)
            throw new InvalidOperationException(
                "Patient reference is required. Call WithPatient() before Build().");

        if (DeviceReference == null)
            throw new InvalidOperationException(
                "Extraction device reference is required. Call WithExtractionDevice() before Build().");

        if (_contents.Count == 0)
            throw new InvalidOperationException(
                "At least one facts content attachment is required. Call WithFactsJson() or WithFactsJsonUrl() before Build().");
    }

    /// <summary>
    ///     Builds the Fact Extraction DocumentReference
    /// </summary>
    /// <returns>The completed DocumentReference resource</returns>
    protected override DocumentReference BuildCore()
    {
        var documentReference = new DocumentReference
        {
            Status = DocumentReferenceStatus.Current,

            // Type: Use LOINC code for fact extraction results
            Type = new CodeableConcept
            {
                Coding = new List<Coding>
                {
                    new()
                    {
                        System = FhirCodingHelper.Systems.LOINC_SYSTEM,
                        Code = "11506-3",
                        Display = "Progress note"
                    }
                },
                Text = "Fact Extraction Results Document"
            },

            // Subject (Patient)
            Subject = PatientReference,

            // Date of creation
            Date = DateTimeOffset.Now,

            // Content attachments
            Content = _contents
        };

        // Add relatesTo for both original and OCR documents
        var relatesTo = new List<DocumentReference.RelatesToComponent>();

        if (_originalDocumentReference != null)
            relatesTo.Add(new DocumentReference.RelatesToComponent
            {
                Code = DocumentRelationshipType.Transforms,
                Target = _originalDocumentReference
            });

        if (_ocrDocumentReference != null)
            relatesTo.Add(new DocumentReference.RelatesToComponent
            {
                Code = DocumentRelationshipType.Transforms,
                Target = _ocrDocumentReference
            });

        if (relatesTo.Any()) documentReference.RelatesTo = relatesTo;

        // Add author (extraction device)
        if (DeviceReference != null)
            documentReference.Author = new List<ResourceReference> { DeviceReference };

        return documentReference;
    }
}