using System.Net;

namespace ThirdOpinion.Common.Aws.HealthLake.Exceptions;

/// <summary>
///     Exception thrown for AWS HealthLake-specific errors
/// </summary>
public class HealthLakeException : FhirResourceException
{
    /// <summary>
    ///     Initializes a new instance of HealthLakeException
    /// </summary>
    /// <param name="message">The error message</param>
    public HealthLakeException(string message) : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of HealthLakeException with an inner exception
    /// </summary>
    /// <param name="message">The error message</param>
    /// <param name="innerException">The inner exception</param>
    public HealthLakeException(string message, Exception innerException) : base(message,
        innerException)
    {
    }

    /// <summary>
    ///     Initializes a new instance of HealthLakeException for a specific resource
    /// </summary>
    /// <param name="message">The error message</param>
    /// <param name="resourceType">The FHIR resource type</param>
    /// <param name="resourceId">The resource identifier</param>
    public HealthLakeException(string message, string resourceType, string resourceId)
        : base(message, resourceType, resourceId)
    {
    }

    /// <summary>
    ///     Initializes a new instance of HealthLakeException with full AWS context
    /// </summary>
    /// <param name="message">The error message</param>
    /// <param name="resourceType">The FHIR resource type</param>
    /// <param name="resourceId">The resource identifier</param>
    /// <param name="statusCode">The HTTP status code</param>
    /// <param name="awsErrorCode">The AWS error code</param>
    /// <param name="awsRequestId">The AWS request ID</param>
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

    /// <summary>
    ///     Initializes a new instance of HealthLakeException with an inner exception and AWS context
    /// </summary>
    /// <param name="message">The error message</param>
    /// <param name="resourceType">The FHIR resource type</param>
    /// <param name="resourceId">The resource identifier</param>
    /// <param name="innerException">The inner exception</param>
    /// <param name="awsErrorCode">The AWS error code</param>
    /// <param name="awsRequestId">The AWS request ID</param>
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

    /// <summary>
    ///     The AWS error code if available
    /// </summary>
    public string? AwsErrorCode { get; protected set; }

    /// <summary>
    ///     The AWS request ID for troubleshooting
    /// </summary>
    public string? AwsRequestId { get; protected set; }
}

/// <summary>
///     Exception thrown when HealthLake datastore access is denied
/// </summary>
public class HealthLakeAccessDeniedException : HealthLakeException
{
    /// <summary>
    ///     Initializes a new instance of HealthLakeAccessDeniedException
    /// </summary>
    /// <param name="message">The error message</param>
    /// <param name="awsRequestId">The AWS request ID</param>
    public HealthLakeAccessDeniedException(string message, string? awsRequestId = null)
        : base(message)
    {
        StatusCode = HttpStatusCode.Forbidden;
        AwsRequestId = awsRequestId;
    }
}

/// <summary>
///     Exception thrown when HealthLake resource conflicts occur (e.g., resource already exists)
/// </summary>
public class HealthLakeConflictException : HealthLakeException
{
    /// <summary>
    ///     Initializes a new instance of HealthLakeConflictException
    /// </summary>
    /// <param name="resourceType">The FHIR resource type</param>
    /// <param name="resourceId">The resource identifier</param>
    /// <param name="message">Optional custom error message</param>
    /// <param name="awsRequestId">The AWS request ID</param>
    public HealthLakeConflictException(string resourceType,
        string resourceId,
        string? message = null,
        string? awsRequestId = null)
        : base(message ?? $"Resource conflict in HealthLake: {resourceType}/{resourceId}",
            resourceType, resourceId, HttpStatusCode.Conflict)
    {
        AwsRequestId = awsRequestId;
    }
}

/// <summary>
///     Exception thrown when HealthLake throttling occurs
/// </summary>
public class HealthLakeThrottlingException : HealthLakeException
{
    /// <summary>
    ///     Initializes a new instance of HealthLakeThrottlingException
    /// </summary>
    /// <param name="message">The error message</param>
    /// <param name="retryAfter">When to retry the request</param>
    /// <param name="awsRequestId">The AWS request ID</param>
    public HealthLakeThrottlingException(string message,
        DateTimeOffset? retryAfter = null,
        string? awsRequestId = null)
        : base(message)
    {
        StatusCode = HttpStatusCode.TooManyRequests;
        RetryAfter = retryAfter;
        AwsRequestId = awsRequestId;
    }

    /// <summary>
    ///     When to retry the request, if provided by HealthLake
    /// </summary>
    public DateTimeOffset? RetryAfter { get; }
}