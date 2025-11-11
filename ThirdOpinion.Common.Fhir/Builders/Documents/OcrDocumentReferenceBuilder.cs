using System.Text;
using Hl7.Fhir.Model;
using ThirdOpinion.Common.Fhir.Builders.Base;
using ThirdOpinion.Common.Fhir.Configuration;
using ThirdOpinion.Common.Fhir.Helpers;

namespace ThirdOpinion.Common.Fhir.Builders.Documents;

/// <summary>
///     Builder for creating FHIR DocumentReference resources for OCR-extracted text documents
/// </summary>
public class OcrDocumentReferenceBuilder : AiResourceBuilderBase<DocumentReference, OcrDocumentReferenceBuilder>
{
    private readonly List<DocumentReference.ContentComponent> _contents;
    private bool _hasInlineContent;
    private bool _hasUrlContent;
    private ResourceReference? _originalDocumentReference;

    /// <summary>
    ///     Creates a new OCR DocumentReference builder
    /// </summary>
    /// <param name="configuration">The AI inference configuration</param>
    public OcrDocumentReferenceBuilder(AiInferenceConfiguration configuration)
        : base(configuration)
    {
        _contents = new List<DocumentReference.ContentComponent>();
        _hasInlineContent = false;
        _hasUrlContent = false;
    }

    /// <summary>
    ///     Sets the OCR device reference that processed the document
    /// </summary>
    /// <param name="device">The OCR device resource reference</param>
    /// <returns>This builder instance for method chaining</returns>
    public OcrDocumentReferenceBuilder WithOcrDevice(ResourceReference device)
    {
        return WithDevice(device);
    }

    /// <summary>
    ///     Sets the OCR device reference that processed the document
    /// </summary>
    /// <param name="deviceId">The OCR device ID</param>
    /// <param name="display">Optional display text</param>
    /// <returns>This builder instance for method chaining</returns>
    public OcrDocumentReferenceBuilder WithOcrDevice(string deviceId, string? display = null)
    {
        return WithDevice(deviceId, display);
    }

    /// <summary>
    ///     Sets the original document that this OCR document transforms
    /// </summary>
    /// <param name="originalDocument">The original document reference</param>
    /// <returns>This builder instance for method chaining</returns>
    public OcrDocumentReferenceBuilder WithOriginalDocument(ResourceReference originalDocument)
    {
        _originalDocumentReference = originalDocument ??
                                     throw new ArgumentNullException(nameof(originalDocument));
        return this;
    }

    /// <summary>
    ///     Sets the original document that this OCR document transforms
    /// </summary>
    /// <param name="documentId">The original document ID</param>
    /// <param name="display">Optional display text</param>
    /// <returns>This builder instance for method chaining</returns>
    public OcrDocumentReferenceBuilder WithOriginalDocument(string documentId,
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
    ///     Adds extracted text content as inline Base64 encoded data
    /// </summary>
    /// <param name="text">The extracted text content</param>
    /// <param name="title">Optional title for the content</param>
    /// <returns>This builder instance for method chaining</returns>
    public OcrDocumentReferenceBuilder WithExtractedText(string text, string? title = null)
    {
        if (string.IsNullOrEmpty(text))
            throw new ArgumentException("Text content cannot be null or empty", nameof(text));

        if (_hasUrlContent)
            throw new InvalidOperationException(
                "Cannot add inline content when URL content has already been set. Use either inline OR URL content, not both.");

        byte[] textBytes = Encoding.UTF8.GetBytes(text);
        string base64Text = Convert.ToBase64String(textBytes);

        var content = new DocumentReference.ContentComponent
        {
            Attachment = new Attachment
            {
                ContentType = "text/plain",
                Data = textBytes,
                Title = title ?? "OCR Extracted Text"
            }
        };

        _contents.Add(content);
        _hasInlineContent = true;
        return this;
    }

    /// <summary>
    ///     Adds extracted text content via S3 URL reference
    /// </summary>
    /// <param name="s3Url">The S3 URL to the extracted text file</param>
    /// <param name="title">Optional title for the content</param>
    /// <returns>This builder instance for method chaining</returns>
    public OcrDocumentReferenceBuilder WithExtractedTextUrl(string s3Url, string? title = null)
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
                ContentType = "text/plain",
                Url = s3Url,
                Title = title ?? "OCR Extracted Text"
            }
        };

        _contents.Add(content);
        _hasUrlContent = true;
        return this;
    }

    /// <summary>
    ///     Adds raw Textract output URL reference
    /// </summary>
    /// <param name="textractRawUrl">The S3 URL to the raw Textract JSON output</param>
    /// <param name="title">Optional title for the content</param>
    /// <returns>This builder instance for method chaining</returns>
    public OcrDocumentReferenceBuilder WithTextractRawUrl(string textractRawUrl,
        string? title = null)
    {
        if (string.IsNullOrWhiteSpace(textractRawUrl))
            throw new ArgumentException("Textract raw URL cannot be null or empty",
                nameof(textractRawUrl));

        if (_hasInlineContent)
            throw new InvalidOperationException(
                "Cannot add URL content when inline content has already been set. Use either inline OR URL content, not both.");

        var content = new DocumentReference.ContentComponent
        {
            Attachment = new Attachment
            {
                ContentType = "application/json",
                Url = textractRawUrl,
                Title = title ?? "Textract Raw Output"
            }
        };

        _contents.Add(content);
        _hasUrlContent = true;
        return this;
    }

    /// <summary>
    ///     Adds simplified Textract output URL reference
    /// </summary>
    /// <param name="textractSimpleUrl">The S3 URL to the simplified Textract output</param>
    /// <param name="title">Optional title for the content</param>
    /// <returns>This builder instance for method chaining</returns>
    public OcrDocumentReferenceBuilder WithTextractSimpleUrl(string textractSimpleUrl,
        string? title = null)
    {
        if (string.IsNullOrWhiteSpace(textractSimpleUrl))
            throw new ArgumentException("Textract simple URL cannot be null or empty",
                nameof(textractSimpleUrl));

        if (_hasInlineContent)
            throw new InvalidOperationException(
                "Cannot add URL content when inline content has already been set. Use either inline OR URL content, not both.");

        var content = new DocumentReference.ContentComponent
        {
            Attachment = new Attachment
            {
                ContentType = "application/json",
                Url = textractSimpleUrl,
                Title = title ?? "Textract Simplified Output"
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
                "OCR device reference is required. Call WithOcrDevice() before Build().");

        if (_originalDocumentReference == null)
            throw new InvalidOperationException(
                "Original document reference is required. Call WithOriginalDocument() before Build().");

        if (_contents.Count == 0)
            throw new InvalidOperationException(
                "At least one content attachment is required. Call WithExtractedText(), WithExtractedTextUrl(), WithTextractRawUrl(), or WithTextractSimpleUrl() before Build().");
    }

    /// <summary>
    ///     Builds the OCR DocumentReference
    /// </summary>
    /// <returns>The completed DocumentReference resource</returns>
    protected override DocumentReference BuildCore()
    {
        var documentReference = new DocumentReference
        {
            Status = DocumentReferenceStatus.Current,

            // Type: Use LOINC code for OCR text document
            Type = new CodeableConcept
            {
                Coding = new List<Coding>
                {
                    new()
                    {
                        System = FhirCodingHelper.Systems.LOINC_SYSTEM,
                        Code = "18842-5",
                        Display = "Discharge summary"
                    }
                },
                Text = "OCR Extracted Text Document"
            },

            // Subject (Patient)
            Subject = PatientReference,

            // Date of creation
            Date = DateTimeOffset.Now,

            // Content attachments
            Content = _contents
        };

        // Add relatesTo for original document with 'transforms' code
        if (_originalDocumentReference != null)
            documentReference.RelatesTo = new List<DocumentReference.RelatesToComponent>
            {
                new()
                {
                    Code = DocumentRelationshipType.Transforms,
                    Target = _originalDocumentReference
                }
            };

        // Add author (OCR device)
        if (DeviceReference != null)
            documentReference.Author = new List<ResourceReference> { DeviceReference };

        return documentReference;
    }
}