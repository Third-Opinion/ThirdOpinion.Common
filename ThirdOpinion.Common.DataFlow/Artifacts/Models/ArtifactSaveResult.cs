namespace ThirdOpinion.Common.DataFlow.Artifacts.Models;

/// <summary>
/// Result of saving an artifact
/// </summary>
public class ArtifactSaveResult
{
    /// <summary>
    /// Whether the save was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Storage path/URI where the artifact was saved
    /// </summary>
    public string? StoragePath { get; set; }

    /// <summary>
    /// Error message if save failed
    /// </summary>
    public string? ErrorMessage { get; set; }
}

