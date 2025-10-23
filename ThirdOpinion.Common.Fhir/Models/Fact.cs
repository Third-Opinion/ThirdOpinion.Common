namespace ThirdOpinion.Common.Fhir.Models;

/// <summary>
/// Represents a clinical fact extracted from documentation
/// </summary>
public class Fact
{
    /// <summary>
    /// Unique identifier for this fact
    /// </summary>
    public string FactGuid { get; set; } = string.Empty;

    /// <summary>
    /// Reference to the document containing this fact
    /// </summary>
    public string FactDocumentReference { get; set; } = string.Empty;

    /// <summary>
    /// Type/category of the fact (e.g., "diagnosis", "treatment", "lab")
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// The actual fact text/content
    /// </summary>
    public string FactText { get; set; } = string.Empty;

    /// <summary>
    /// Array of reference strings pointing to related content
    /// </summary>
    public string[] Ref { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Time reference for when this fact was recorded or is relevant
    /// </summary>
    public string TimeRef { get; set; } = string.Empty;

    /// <summary>
    /// Description of why this fact is relevant to the assessment
    /// </summary>
    public string Relevance { get; set; } = string.Empty;
}