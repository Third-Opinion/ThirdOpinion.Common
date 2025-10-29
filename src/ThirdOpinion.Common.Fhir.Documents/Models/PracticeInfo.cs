namespace ThirdOpinion.Common.Fhir.Documents.Models;

/// <summary>
/// Information about a medical practice for organizing documents
/// </summary>
public class PracticeInfo
{
    public required string Id { get; set; }
    public required string Name { get; set; }

    /// <summary>
    /// Gets the folder name format: {PracticeName}_{PracticeId}
    /// </summary>
    public string GetFolderName()
    {
        return $"{Name}_{Id}";
    }
}

/// <summary>
/// S3 tag set for document metadata
/// </summary>
public class S3TagSet
{
    public Dictionary<string, string> Tags { get; set; } = new();

    public void AddTag(string key, string value)
    {
        if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
        {
            Tags[key] = value;
        }
    }

    public void AddCategoryTag(List<CodeableConcept> categories)
    {
        var firstCategory = categories
            .SelectMany(c => c.Coding)
            .Where(c => c.System == "http://hl7.org/fhir/us/core/CodeSystem/us-core-documentreference-category")
            .FirstOrDefault();

        if (firstCategory?.Code != null)
        {
            AddTag("us-core-documentreference-category", firstCategory.Code);
        }

        if (categories.SelectMany(c => c.Coding).Count() > 1)
        {
            // Log warning about multiple categories
        }
    }

    public void AddEncounterTag(List<EncounterReference> encounters)
    {
        var firstEncounter = encounters.FirstOrDefault();
        if (firstEncounter?.Reference != null)
        {
            AddTag("encounter.reference", firstEncounter.Reference);
        }

        if (encounters.Count > 1)
        {
            // Log warning about multiple encounters
        }
    }

    public void AddMetaTags(DocumentMeta? meta)
    {
        if (meta?.LastUpdated != null)
        {
            AddTag("meta.lastUpdated", meta.LastUpdated);
        }

        if (meta?.VersionId != null)
        {
            AddTag("meta.versionId", meta.VersionId);
        }
    }

    public void AddStatusTag(string status)
    {
        AddTag("status", status);
    }
}