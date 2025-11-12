using ThirdOpinion.Common.DataFlow.Models;

namespace ThirdOpinion.Common.DataFlow.Core;

/// <summary>
/// Options for capturing artifacts from a pipeline step
/// </summary>
public class ArtifactOptions<T>
{
    /// <summary>
    /// Static artifact name (used if ArtifactNameFactory is not provided)
    /// </summary>
    public string? ArtifactName { get; set; }

    /// <summary>
    /// Factory to generate artifact name from data
    /// </summary>
    public Func<T, string>? ArtifactNameFactory { get; set; }

    /// <summary>
    /// Function to extract resource ID from data
    /// </summary>
    public Func<T, string>? GetResourceId { get; set; }

    /// <summary>
    /// Function to extract artifact data from the result (defaults to the result itself)
    /// </summary>
    public Func<T, object>? GetArtifactData { get; set; }

    /// <summary>
    /// Storage type for the artifact
    /// </summary>
    public ArtifactStorageType StorageType { get; set; } = ArtifactStorageType.S3;
}

