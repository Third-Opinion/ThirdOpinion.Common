using System.Text.Json.Serialization;

namespace ThirdOpinion.Common.Fhir.Documents.Models;

/// <summary>
///     Represents a FHIR Bundle response from Patient/$everything operation
/// </summary>
public class BundleData
{
    [JsonPropertyName("resourceType")]
    public string ResourceType { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("total")]
    public int? Total { get; set; }

    [JsonPropertyName("link")]
    public List<BundleLink> Link { get; set; } = new();

    [JsonPropertyName("entry")]
    public List<BundleEntry> Entry { get; set; } = new();

    /// <summary>
    ///     Gets the next page URL if pagination is available
    /// </summary>
    public string? GetNextPageUrl()
    {
        return Link
            .FirstOrDefault(l => l.Relation == "next")?.Url;
    }

    /// <summary>
    ///     Extracts DocumentReference resources from bundle entries
    /// </summary>
    public List<DocumentReferenceData> GetDocumentReferences()
    {
        return Entry
            .Where(e => e.Resource?.ResourceType == "DocumentReference")
            .Select(e => e.Resource!)
            .ToList();
    }

    /// <summary>
    ///     Validates that this is a searchset Bundle
    /// </summary>
    public bool IsValidSearchsetBundle()
    {
        return ResourceType == "Bundle" && Type == "searchset";
    }
}

public class BundleLink
{
    [JsonPropertyName("relation")]
    public string Relation { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

public class BundleEntry
{
    [JsonPropertyName("resource")]
    public DocumentReferenceData? Resource { get; set; }

    [JsonPropertyName("search")]
    public SearchInfo? Search { get; set; }
}

public class SearchInfo
{
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = string.Empty;
}