using Hl7.Fhir.Model;
using Newtonsoft.Json;
using ThirdOpinion.Common.Fhir.Models;

namespace ThirdOpinion.Common.Fhir.Extensions;

/// <summary>
///     Helper class for creating FHIR extensions from clinical facts
/// </summary>
public static class ClinicalFactExtension
{
    /// <summary>
    ///     The extension URL for clinical facts
    /// </summary>
    public const string ExtensionUrl = "https://thirdopinion.io/clinical-fact";

    /// <summary>
    ///     The sub-extension URL for NDJSON facts array
    /// </summary>
    public const string NdjsonExtensionUrl = "factsArrayJson";

    /// <summary>
    ///     Creates a FHIR Extension from a clinical fact
    /// </summary>
    /// <param name="fact">The clinical fact to convert</param>
    /// <returns>A FHIR Extension representing the clinical fact</returns>
    public static Extension CreateExtension(Fact fact)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));

        var extension = new Extension
        {
            Url = ExtensionUrl
        };

        // Add fact GUID
        if (!string.IsNullOrWhiteSpace(fact.factGuid))
            extension.Extension.Add(new Extension("factGuid", new FhirString(fact.factGuid)));

        // Add document reference
        if (!string.IsNullOrWhiteSpace(fact.factDocumentReference))
            extension.Extension.Add(new Extension("factDocumentReference",
                new FhirString(fact.factDocumentReference)));

        // Add fact type
        if (!string.IsNullOrWhiteSpace(fact.type))
            extension.Extension.Add(new Extension("type", new FhirString(fact.type)));

        // Add fact text
        if (!string.IsNullOrWhiteSpace(fact.fact))
            extension.Extension.Add(new Extension("fact", new FhirString(fact.fact)));

        // Add references array
        if (fact.@ref != null && fact.@ref.Length > 0)
            foreach (string refValue in fact.@ref.Where(r => !string.IsNullOrWhiteSpace(r)))
                extension.Extension.Add(new Extension("ref", new FhirString(refValue)));

        // Add time reference
        if (!string.IsNullOrWhiteSpace(fact.timeRef))
            extension.Extension.Add(new Extension("timeRef", new FhirString(fact.timeRef)));

        // Add relevance
        if (!string.IsNullOrWhiteSpace(fact.relevance))
            extension.Extension.Add(new Extension("relevance", new FhirString(fact.relevance)));

        return extension;
    }

    /// <summary>
    ///     Creates FHIR Extensions from multiple clinical facts
    /// </summary>
    /// <param name="facts">The clinical facts to convert</param>
    /// <returns>A list of FHIR Extensions representing the clinical facts</returns>
    public static List<Extension> CreateExtensions(IEnumerable<Fact> facts)
    {
        if (facts == null)
            return new List<Extension>();

        return new List<Extension> { CreateNdjsonExtension(facts) };
    }

    /// <summary>
    ///     Creates FHIR Extensions from an array of clinical facts
    /// </summary>
    /// <param name="facts">The clinical facts to convert</param>
    /// <returns>A list of FHIR Extensions representing the clinical facts</returns>
    public static List<Extension> CreateExtensions(params Fact[] facts)
    {
        return CreateExtensions((IEnumerable<Fact>)facts);
    }

    /// <summary>
    ///     Creates a single FHIR Extension containing all facts as NDJSON (newline-delimited JSON)
    /// </summary>
    /// <param name="facts">The clinical facts to convert</param>
    /// <returns>A FHIR Extension with all facts as NDJSON in a single valueString</returns>
    public static Extension CreateNdjsonExtension(IEnumerable<Fact> facts)
    {
        if (facts == null)
            throw new ArgumentNullException(nameof(facts));

        var factsList = facts.Where(f => f != null).ToList();

        if (factsList.Count == 0)
            throw new ArgumentException("Facts collection cannot be empty", nameof(facts));

        // Serialize each fact to a single-line JSON string
        var ndjsonLines = factsList.Select(fact =>
            JsonConvert.SerializeObject(fact, Formatting.None));

        // Join with newlines to create NDJSON format
        string ndjsonContent = string.Join("\n", ndjsonLines);

        // Create the main extension with sub-extension containing NDJSON
        var extension = new Extension
        {
            Url = ExtensionUrl
        };

        extension.Extension.Add(new Extension(NdjsonExtensionUrl, new FhirString(ndjsonContent)));

        return extension;
    }

    /// <summary>
    ///     Creates a single FHIR Extension containing all facts as NDJSON (newline-delimited JSON)
    /// </summary>
    /// <param name="facts">The clinical facts to convert</param>
    /// <returns>A FHIR Extension with all facts as NDJSON in a single valueString</returns>
    public static Extension CreateNdjsonExtension(params Fact[] facts)
    {
        return CreateNdjsonExtension((IEnumerable<Fact>)facts);
    }
}