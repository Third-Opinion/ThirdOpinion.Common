namespace ThirdOpinion.Common.Fhir.Documents.Models;

/// <summary>
/// Represents a DocumentReference record from CSV for downloading from HealthLake
/// </summary>
public class DocumentReferenceRecord
{
    /// <summary>
    /// The DocumentReference ID from HealthLake - this is the primary identifier for direct download
    /// </summary>
    public required string DocumentReferenceId { get; set; }

    /// <summary>
    /// Row number in the CSV file for tracking
    /// </summary>
    public int RowNumber { get; set; }

    /// <summary>
    /// Optional Patient ID - for backward compatibility and logging purposes
    /// This will be populated if the CSV contains both DocumentReferenceId and PatientId
    /// </summary>
    public string? PatientId { get; set; }

    public override string ToString()
    {
        return $"Row {RowNumber}: DocumentReference {DocumentReferenceId}" +
               (PatientId != null ? $" (Patient {PatientId})" : "");
    }
}

/// <summary>
/// Progress information for DocumentReference CSV processing
/// </summary>
public class DocumentProcessingProgress
{
    public int ProcessedCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public int TotalCount { get; set; }
    public int CurrentRow { get; set; }
    public string? CurrentDocumentId { get; set; }
    public DateTime StartTime { get; set; }
    public TimeSpan? EstimatedTimeRemaining { get; set; }
    public double ProcessingRate { get; set; } // documents per second

    public string Message => $"Processing DocumentReference {CurrentDocumentId} " +
                           $"(Row {CurrentRow}) - {SuccessCount}/{ProcessedCount} successful";
}

/// <summary>
/// Result of DocumentReference CSV validation
/// </summary>
public class DocumentCsvValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public int TotalRows { get; set; }
    public string[] Headers { get; set; } = Array.Empty<string>();
    public bool HasDocumentReferenceIdColumn => Headers.Contains("DocumentReferenceId", StringComparer.OrdinalIgnoreCase) ||
                                                Headers.Contains("documentReferenceId", StringComparer.OrdinalIgnoreCase) ||
                                                Headers.Contains("document_reference_id", StringComparer.OrdinalIgnoreCase) ||
                                                Headers.Contains("DocumentId", StringComparer.OrdinalIgnoreCase) ||
                                                Headers.Contains("documentId", StringComparer.OrdinalIgnoreCase) ||
                                                Headers.Contains("document_id", StringComparer.OrdinalIgnoreCase) ||
                                                Headers.Contains("id", StringComparer.OrdinalIgnoreCase);

    public bool HasPatientIdColumn => Headers.Contains("PatientId", StringComparer.OrdinalIgnoreCase) ||
                                      Headers.Contains("patientId", StringComparer.OrdinalIgnoreCase) ||
                                      Headers.Contains("patient_id", StringComparer.OrdinalIgnoreCase);
}