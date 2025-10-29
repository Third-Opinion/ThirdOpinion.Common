using System.Text.Json.Serialization;

namespace ThirdOpinion.Common.Fhir.Documents.Models;

/// <summary>
/// Represents a FHIR DocumentReference resource with relevant fields for download processing
/// </summary>
public class DocumentReferenceData
{
    [JsonPropertyName("resourceType")]
    public string ResourceType { get; set; } = "DocumentReference";

    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "current"; // Default to "current" if not provided

    [JsonPropertyName("subject")]
    public SubjectReference? Subject { get; set; }

    [JsonPropertyName("content")]
    public List<DocumentContent> Content { get; set; } = new();

    [JsonPropertyName("category")]
    public List<CodeableConcept> Category { get; set; } = new();

    [JsonPropertyName("type")]
    public CodeableConcept? Type { get; set; }

    [JsonPropertyName("context")]
    public DocumentContext? Context { get; set; }

    [JsonPropertyName("meta")]
    public DocumentMeta? Meta { get; set; }

    [JsonPropertyName("extension")]
    public List<Extension> Extension { get; set; } = new();

    [JsonPropertyName("date")]
    public string? Date { get; set; }

    /// <summary>
    /// Extracts patient ID from subject reference
    /// </summary>
    public string? GetPatientId()
    {
        return Subject?.Reference?.Split('/').LastOrDefault();
    }

    /// <summary>
    /// Gets practice information from extensions
    /// </summary>
    public PracticeInfo? GetPracticeInfo()
    {
        var practiceExtension = Extension
            .FirstOrDefault(e => e.Url == "https://fhir.athena.io/StructureDefinition/ah-practice");

        if (practiceExtension?.ValueReference?.Reference == null)
            return null;

        var practiceReference = practiceExtension.ValueReference.Reference;
        var parts = practiceReference.Split('/').LastOrDefault()?.Split('-');

        if (parts?.Length >= 3)
        {
            return new PracticeInfo
            {
                Id = parts[2],
                Name = "HMU" // Will be resolved later
            };
        }

        return null;
    }
}

public class SubjectReference
{
    [JsonPropertyName("reference")]
    public string? Reference { get; set; }
}

public class DocumentContent
{
    [JsonPropertyName("attachment")]
    public Attachment? Attachment { get; set; }
}

public class Attachment
{
    [JsonPropertyName("contentType")]
    public string? ContentType { get; set; }

    [JsonPropertyName("data")]
    public string? Data { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>
    /// Determines if this is embedded base64 content or a binary reference
    /// </summary>
    public bool IsEmbeddedContent => !string.IsNullOrEmpty(Data);

    /// <summary>
    /// Extracts Binary ID from URL if this is a binary reference
    /// </summary>
    public string? GetBinaryId()
    {
        if (string.IsNullOrEmpty(Url) || IsEmbeddedContent)
            return null;

        return Url.Split('/').LastOrDefault();
    }
}

public class CodeableConcept
{
    [JsonPropertyName("coding")]
    public List<Coding> Coding { get; set; } = new();
}

public class Coding
{
    [JsonPropertyName("system")]
    public string? System { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("display")]
    public string? Display { get; set; }
}

public class DocumentContext
{
    [JsonPropertyName("encounter")]
    public List<EncounterReference> Encounter { get; set; } = new();
}

public class EncounterReference
{
    [JsonPropertyName("reference")]
    public string? Reference { get; set; }
}

public class DocumentMeta
{
    [JsonPropertyName("lastUpdated")]
    public string? LastUpdated { get; set; }

    [JsonPropertyName("versionId")]
    public string? VersionId { get; set; }
}

public class Extension
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("valueReference")]
    public ValueReference? ValueReference { get; set; }
}

public class ValueReference
{
    [JsonPropertyName("reference")]
    public string? Reference { get; set; }
}