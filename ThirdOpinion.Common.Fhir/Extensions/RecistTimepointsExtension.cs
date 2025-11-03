using Hl7.Fhir.Model;

namespace ThirdOpinion.Common.Fhir.Extensions;

/// <summary>
///     Helper class for creating FHIR extensions from RECIST timepoints JSON data
/// </summary>
public static class RecistTimepointsExtension
{
    /// <summary>
    ///     The extension URL for RECIST timepoints data
    /// </summary>
    public const string ExtensionUrl = "https://thirdopinion.io/recist-timepoints";

    /// <summary>
    ///     Creates a FHIR Extension from RECIST timepoints JSON data
    /// </summary>
    /// <param name="timepointsJson">The RECIST timepoints JSON string to store</param>
    /// <returns>A FHIR Extension containing the RECIST timepoints data</returns>
    /// <exception cref="ArgumentNullException">Thrown when timepointsJson is null</exception>
    /// <exception cref="ArgumentException">Thrown when timepointsJson is empty or whitespace</exception>
    public static Extension CreateExtension(string timepointsJson)
    {
        if (timepointsJson == null)
            throw new ArgumentNullException(nameof(timepointsJson));

        if (string.IsNullOrWhiteSpace(timepointsJson))
            throw new ArgumentException("RECIST timepoints JSON cannot be empty or whitespace", nameof(timepointsJson));

        var extension = new Extension
        {
            Url = ExtensionUrl
        };

        // Add the timepoints JSON as a nested extension
        extension.Extension.Add(new Extension("timepointsJson", new FhirString(timepointsJson)));

        return extension;
    }
}
