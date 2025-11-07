namespace ThirdOpinion.Common.Fhir.Documents.Models;

/// <summary>
///     Result of a document download operation
/// </summary>
public class DocumentDownloadResult
{
    public int SuccessfulDownloads { get; set; }
    public int FailedDownloads { get; set; }
    public int TotalDocumentsProcessed { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<DocumentDownloadInfo> DownloadedDocuments { get; set; } = new();

    public bool IsSuccess => FailedDownloads == 0 && Errors.Count == 0;

    public double SuccessRate => TotalDocumentsProcessed > 0
        ? (double)SuccessfulDownloads / TotalDocumentsProcessed
        : 0.0;
}

/// <summary>
///     Information about a successfully downloaded document
/// </summary>
public class DocumentDownloadInfo
{
    public required string DocumentId { get; set; }
    public required string PatientId { get; set; }
    public required string S3Key { get; set; }
    public required string ContentType { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime DownloadedAt { get; set; }
    public Dictionary<string, string> S3Tags { get; set; } = new();
}