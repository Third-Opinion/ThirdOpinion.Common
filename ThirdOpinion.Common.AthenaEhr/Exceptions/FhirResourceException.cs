using System.Net;

namespace ThirdOpinion.Common.AthenaEhr;

/// <summary>
///     Base exception for FHIR resource operations
/// </summary>
public class FhirResourceException : Exception
{
    public FhirResourceException(string message) : base(message)
    {
    }

    public FhirResourceException(string message, Exception innerException) : base(message,
        innerException)
    {
    }

    public FhirResourceException(string message, string resourceType, string resourceId) :
        base(message)
    {
        ResourceType = resourceType;
        ResourceId = resourceId;
    }

    public FhirResourceException(string message,
        string resourceType,
        string resourceId,
        HttpStatusCode statusCode)
        : base(message)
    {
        ResourceType = resourceType;
        ResourceId = resourceId;
        StatusCode = statusCode;
    }

    public FhirResourceException(string message,
        string resourceType,
        string resourceId,
        Exception innerException)
        : base(message, innerException)
    {
        ResourceType = resourceType;
        ResourceId = resourceId;
    }

    /// <summary>
    ///     The FHIR resource type involved in the operation
    /// </summary>
    public string? ResourceType { get; }

    /// <summary>
    ///     The resource ID involved in the operation
    /// </summary>
    public string? ResourceId { get; }

    /// <summary>
    ///     HTTP status code if applicable
    /// </summary>
    public HttpStatusCode? StatusCode { get; protected set; }
}

/// <summary>
///     Exception thrown when a FHIR resource is not found
/// </summary>
public class FhirResourceNotFoundException : FhirResourceException
{
    public FhirResourceNotFoundException(string resourceType, string resourceId)
        : base($"FHIR resource not found: {resourceType}/{resourceId}", resourceType, resourceId,
            HttpStatusCode.NotFound)
    {
    }
}

/// <summary>
///     Exception thrown when authentication fails for FHIR resource access
/// </summary>
public class FhirAuthenticationException : FhirResourceException
{
    public FhirAuthenticationException(string message)
        : base(message)
    {
        StatusCode = HttpStatusCode.Unauthorized;
    }

    public FhirAuthenticationException(string message, Exception innerException)
        : base(message, innerException)
    {
        StatusCode = HttpStatusCode.Unauthorized;
    }
}

/// <summary>
///     Exception thrown when rate limiting is encountered
/// </summary>
public class FhirRateLimitException : FhirResourceException
{
    public FhirRateLimitException(string message, DateTimeOffset? retryAfter = null)
        : base(message)
    {
        StatusCode = HttpStatusCode.TooManyRequests;
        RetryAfter = retryAfter;
    }

    /// <summary>
    ///     When the rate limit will reset, if provided by the server
    /// </summary>
    public DateTimeOffset? RetryAfter { get; }
}