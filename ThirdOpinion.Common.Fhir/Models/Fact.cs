using System.Text.Json;

namespace ThirdOpinion.Common.Fhir.Models;

/// <summary>
/// Represents a clinical fact extracted from documentation
/// </summary>
public class Fact
{
    /// <summary>
    /// Unique identifier for this fact
    /// </summary>
    public string factGuid { get; set; } = string.Empty;

    /// <summary>
    /// Reference to the document containing this fact
    /// </summary>
    public string factDocumentReference { get; set; } = string.Empty;

    /// <summary>
    /// Type/category of the fact (e.g., "diagnosis", "treatment", "lab")
    /// </summary>
    public string type { get; set; } = string.Empty;

    /// <summary>
    /// The actual fact text/content
    /// </summary>
    public string fact { get; set; } = string.Empty;

    /// <summary>
    /// Array of reference strings pointing to related content
    /// </summary>
    public string[] @ref { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Time reference for when this fact was recorded or is relevant
    /// </summary>
    public string timeRef { get; set; } = string.Empty;

    /// <summary>
    /// Description of why this fact is relevant to the assessment
    /// </summary>
    public string relevance { get; set; } = string.Empty;

    /// <summary>
    /// Factory method to create a list of Facts from a JSON array
    /// </summary>
    /// <param name="factsJson">JSON array string containing facts</param>
    /// <returns>List of Fact objects</returns>
    /// <exception cref="JsonException">Thrown when JSON is invalid</exception>
    /// <exception cref="ArgumentNullException">Thrown when factsJson is null</exception>
    public static List<Fact> FromJsonArray(string factsJson)
    {
        if (string.IsNullOrWhiteSpace(factsJson))
        {
            throw new ArgumentNullException(nameof(factsJson), "Facts JSON cannot be null or empty");
        }

        try
        {
            var facts = JsonSerializer.Deserialize<Fact[]>(factsJson);
            return facts?.ToList() ?? new List<Fact>();
        }
        catch (JsonException ex)
        {
            throw new JsonException($"Failed to deserialize facts JSON: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Factory method to create a list of Facts from a Fact array
    /// </summary>
    /// <param name="factsArray">Array of Fact objects</param>
    /// <returns>List of Fact objects</returns>
    public static List<Fact> FromFactsArray(Fact[] factsArray)
    {
        return factsArray?.ToList() ?? new List<Fact>();
    }
}