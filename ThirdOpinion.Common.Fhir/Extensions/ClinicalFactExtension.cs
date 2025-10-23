using Hl7.Fhir.Model;
using ThirdOpinion.Common.Fhir.Models;

namespace ThirdOpinion.Common.Fhir.Extensions;

/// <summary>
/// Helper class for creating FHIR extensions from clinical facts
/// </summary>
public static class ClinicalFactExtension
{
    /// <summary>
    /// The extension URL for clinical facts
    /// </summary>
    public const string ExtensionUrl = "https://thirdopinion.io/clinical-fact";

    /// <summary>
    /// Creates a FHIR Extension from a clinical fact
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
        if (!string.IsNullOrWhiteSpace(fact.FactGuid))
        {
            extension.Extension.Add(new Extension("factGuid", new FhirString(fact.FactGuid)));
        }

        // Add document reference
        if (!string.IsNullOrWhiteSpace(fact.FactDocumentReference))
        {
            extension.Extension.Add(new Extension("factDocumentReference", new FhirString(fact.FactDocumentReference)));
        }

        // Add fact type
        if (!string.IsNullOrWhiteSpace(fact.Type))
        {
            extension.Extension.Add(new Extension("type", new FhirString(fact.Type)));
        }

        // Add fact text
        if (!string.IsNullOrWhiteSpace(fact.FactText))
        {
            extension.Extension.Add(new Extension("fact", new FhirString(fact.FactText)));
        }

        // Add references array
        if (fact.Ref != null && fact.Ref.Length > 0)
        {
            foreach (var refValue in fact.Ref.Where(r => !string.IsNullOrWhiteSpace(r)))
            {
                extension.Extension.Add(new Extension("ref", new FhirString(refValue)));
            }
        }

        // Add time reference
        if (!string.IsNullOrWhiteSpace(fact.TimeRef))
        {
            extension.Extension.Add(new Extension("timeRef", new FhirString(fact.TimeRef)));
        }

        // Add relevance
        if (!string.IsNullOrWhiteSpace(fact.Relevance))
        {
            extension.Extension.Add(new Extension("relevance", new FhirString(fact.Relevance)));
        }

        return extension;
    }

    /// <summary>
    /// Creates FHIR Extensions from multiple clinical facts
    /// </summary>
    /// <param name="facts">The clinical facts to convert</param>
    /// <returns>A list of FHIR Extensions representing the clinical facts</returns>
    public static List<Extension> CreateExtensions(IEnumerable<Fact> facts)
    {
        if (facts == null)
            return new List<Extension>();

        return facts.Where(f => f != null)
                   .Select(CreateExtension)
                   .ToList();
    }

    /// <summary>
    /// Creates FHIR Extensions from an array of clinical facts
    /// </summary>
    /// <param name="facts">The clinical facts to convert</param>
    /// <returns>A list of FHIR Extensions representing the clinical facts</returns>
    public static List<Extension> CreateExtensions(params Fact[] facts)
    {
        return CreateExtensions((IEnumerable<Fact>)facts);
    }
}