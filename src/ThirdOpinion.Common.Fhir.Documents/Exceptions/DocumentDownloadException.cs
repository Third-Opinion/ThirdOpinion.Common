namespace ThirdOpinion.Common.Fhir.Documents.Exceptions;

/// <summary>
/// Category of error for exception classification
/// </summary>
public enum ErrorCategory
{
    BusinessLogic,
    Infrastructure,
    Configuration,
    Validation
}

/// <summary>
/// Exception thrown when document download operations fail
/// </summary>
public class DocumentDownloadException : Exception
{
    public bool IsRetriable { get; }
    public ErrorCategory Category { get; }
    public string? PatientId { get; set; }
    public string? DocumentId { get; set; }
    public string? Operation { get; set; }

    public DocumentDownloadException(
        string message,
        bool isRetriable = false,
        ErrorCategory category = ErrorCategory.Infrastructure)
        : base(message)
    {
        IsRetriable = isRetriable;
        Category = category;
    }

    public DocumentDownloadException(
        string message,
        Exception innerException,
        bool isRetriable = false,
        ErrorCategory category = ErrorCategory.Infrastructure)
        : base(message, innerException)
    {
        IsRetriable = isRetriable;
        Category = category;
    }

    public DocumentDownloadException(
        string message,
        string patientId,
        string documentId,
        Exception? innerException = null)
        : base(message, innerException)
    {
        PatientId = patientId;
        DocumentId = documentId;
        IsRetriable = false;
        Category = ErrorCategory.BusinessLogic;
    }
}