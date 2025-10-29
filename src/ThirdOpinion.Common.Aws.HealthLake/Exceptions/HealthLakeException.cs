using System.Net;

namespace ThirdOpinion.Common.Aws.HealthLake.Exceptions;

/// <summary>
/// Exception thrown for AWS HealthLake-specific errors
/// </summary>
public class HealthLakeException : FhirResourceException
{
    /// <summary>
    /// The AWS error code if available
    /// </summary>
    public string? AwsErrorCode { get; protected set; }

    /// <summary>
    /// The AWS request ID for troubleshooting
    /// </summary>
    public string? AwsRequestId { get; protected set; }

    public HealthLakeException(string message) : base(message)
    {
    }

    public HealthLakeException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public HealthLakeException(string message, string resourceType, string resourceId) 
        : base(message, resourceType, resourceId)
    {
    }

    public HealthLakeException(
        string message, 
        string resourceType, 
        string resourceId, 
        HttpStatusCode statusCode,
        string? awsErrorCode = null,
        string? awsRequestId = null) 
        : base(message, resourceType, resourceId, statusCode)
    {
        AwsErrorCode = awsErrorCode;
        AwsRequestId = awsRequestId;
    }

    public HealthLakeException(
        string message, 
        string resourceType, 
        string resourceId, 
        Exception innerException,
        string? awsErrorCode = null,
        string? awsRequestId = null) 
        : base(message, resourceType, resourceId, innerException)
    {
        AwsErrorCode = awsErrorCode;
        AwsRequestId = awsRequestId;
    }
}

/// <summary>
/// Exception thrown when HealthLake datastore access is denied
/// </summary>
public class HealthLakeAccessDeniedException : HealthLakeException
{
    public HealthLakeAccessDeniedException(string message, string? awsRequestId = null)
        : base(message)
    {
        StatusCode = HttpStatusCode.Forbidden;
        AwsRequestId = awsRequestId;
    }
}

/// <summary>
/// Exception thrown when HealthLake resource conflicts occur (e.g., resource already exists)
/// </summary>
public class HealthLakeConflictException : HealthLakeException
{
    public HealthLakeConflictException(string resourceType, string resourceId, string? message = null, string? awsRequestId = null)
        : base(message ?? $"Resource conflict in HealthLake: {resourceType}/{resourceId}", resourceType, resourceId, HttpStatusCode.Conflict)
    {
        AwsRequestId = awsRequestId;
    }
}

/// <summary>
/// Exception thrown when HealthLake throttling occurs
/// </summary>
public class HealthLakeThrottlingException : HealthLakeException
{
    /// <summary>
    /// When to retry the request, if provided by HealthLake
    /// </summary>
    public DateTimeOffset? RetryAfter { get; }

    public HealthLakeThrottlingException(string message, DateTimeOffset? retryAfter = null, string? awsRequestId = null)
        : base(message)
    {
        StatusCode = HttpStatusCode.TooManyRequests;
        RetryAfter = retryAfter;
        AwsRequestId = awsRequestId;
    }
}