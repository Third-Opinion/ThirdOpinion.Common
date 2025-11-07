namespace ThirdOpinion.Common.Fhir.Helpers;

/// <summary>
///     Provides methods for generating FHIR resource IDs with consistent formatting
/// </summary>
public static class FhirIdGenerator
{
    private static int _sequenceCounter;
    private static readonly object _sequenceLock = new();

    /// <summary>
    ///     Generates an inference ID with GUID format: to.ai-inference-{guid}
    /// </summary>
    /// <returns>A unique inference ID</returns>
    public static string GenerateInferenceId()
    {
        return $"to.ai-inference-{Guid.NewGuid().ToString().ToLowerInvariant()}";
    }

    /// <summary>
    ///     Generates an inference ID with sequential number format: to.ai-inference-{number:D6}
    /// </summary>
    /// <param name="sequenceNumber">The sequence number to use</param>
    /// <returns>A sequential inference ID</returns>
    public static string GenerateInferenceId(int sequenceNumber)
    {
        return $"to.ai-inference-{sequenceNumber:D6}";
    }

    /// <summary>
    ///     Generates a provenance ID with GUID format: to.ai-provenance-{guid}
    /// </summary>
    /// <returns>A unique provenance ID</returns>
    public static string GenerateProvenanceId()
    {
        return $"to.ai-provenance-{Guid.NewGuid().ToString().ToLowerInvariant()}";
    }

    /// <summary>
    ///     Generates a document ID with type and GUID format: to.ai-document-{type}-{guid}
    /// </summary>
    /// <param name="type">The document type</param>
    /// <returns>A unique document ID</returns>
    public static string GenerateDocumentId(string type)
    {
        ArgumentException.ThrowIfNullOrEmpty(type);
        string sanitizedType = type.ToLowerInvariant().Replace(" ", "-");
        return $"to.ai-document-{sanitizedType}-{Guid.NewGuid().ToString().ToLowerInvariant()}";
    }

    /// <summary>
    ///     Generates a resource ID with custom prefix and optional GUID
    /// </summary>
    /// <param name="prefix">The prefix for the ID</param>
    /// <param name="guid">Optional GUID to use, generates new if null</param>
    /// <returns>A unique resource ID</returns>
    public static string GenerateResourceId(string prefix, string? guid = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(prefix);
        string guidToUse = guid ?? Guid.NewGuid().ToString();
        return $"{prefix}-{guidToUse.ToLowerInvariant()}";
    }

    /// <summary>
    ///     Generates a sequential ID with auto-incrementing number
    /// </summary>
    /// <param name="prefix">The prefix for the ID</param>
    /// <returns>A sequential ID with incremented number</returns>
    public static string GenerateSequentialId(string prefix)
    {
        ArgumentException.ThrowIfNullOrEmpty(prefix);

        lock (_sequenceLock)
        {
            _sequenceCounter++;
            return $"{prefix}-{_sequenceCounter:D6}";
        }
    }

    /// <summary>
    ///     Generates a sequential ID with specified sequence number
    /// </summary>
    /// <param name="prefix">The prefix for the ID</param>
    /// <param name="sequence">The sequence number to use</param>
    /// <returns>A sequential ID with specified number</returns>
    public static string GenerateSequentialId(string prefix, int? sequence = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(prefix);

        if (sequence.HasValue) return $"{prefix}-{sequence.Value:D6}";

        return GenerateSequentialId(prefix);
    }

    /// <summary>
    ///     Resets the internal sequence counter (useful for testing)
    /// </summary>
    public static void ResetSequenceCounter()
    {
        lock (_sequenceLock)
        {
            _sequenceCounter = 0;
        }
    }
}