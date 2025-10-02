using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace ThirdOpinion.Common.UnitTests.TestDataBuilders;

/// <summary>
///     Base class for test data builders following the Builder pattern.
///     Provides common functionality for creating test objects.
/// </summary>
public abstract class TestDataBuilder<T>
{
    protected abstract T BuildInternal();

    public T Build()
    {
        return BuildInternal();
    }

    public static implicit operator T(TestDataBuilder<T> builder)
    {
        return builder.Build();
    }
}

/// <summary>
///     Builder for creating ClaimsPrincipal objects for testing authentication scenarios
/// </summary>
public class ClaimsPrincipalBuilder : TestDataBuilder<ClaimsPrincipal>
{
    private readonly List<Claim> _claims = new();
    private string _authenticationType = "Test";

    public static ClaimsPrincipalBuilder Create()
    {
        return new ClaimsPrincipalBuilder();
    }

    public ClaimsPrincipalBuilder WithClaim(string type, string value)
    {
        _claims.Add(new Claim(type, value));
        return this;
    }

    public ClaimsPrincipalBuilder WithPersonGuid(Guid personGuid)
    {
        return WithClaim("person_guid", personGuid.ToString());
    }

    public ClaimsPrincipalBuilder WithTenantGuid(Guid tenantGuid)
    {
        return WithClaim("tenant_guid", tenantGuid.ToString());
    }

    public ClaimsPrincipalBuilder WithUserId(string userId)
    {
        return WithClaim(ClaimTypes.NameIdentifier, userId);
    }

    public ClaimsPrincipalBuilder WithEmail(string email)
    {
        return WithClaim(ClaimTypes.Email, email);
    }

    public ClaimsPrincipalBuilder WithAuthenticationType(string authenticationType)
    {
        _authenticationType = authenticationType;
        return this;
    }

    protected override ClaimsPrincipal BuildInternal()
    {
        var identity = new ClaimsIdentity(_claims, _authenticationType);
        return new ClaimsPrincipal(identity);
    }
}

/// <summary>
///     Builder for creating HttpContext objects for testing web scenarios
/// </summary>
public class HttpContextBuilder : TestDataBuilder<HttpContext>
{
    private readonly Dictionary<string, object> _items = new();
    private ClaimsPrincipal? _user;

    public static HttpContextBuilder Create()
    {
        return new HttpContextBuilder();
    }

    public HttpContextBuilder WithUser(ClaimsPrincipal user)
    {
        _user = user;
        return this;
    }

    public HttpContextBuilder WithUser(ClaimsPrincipalBuilder userBuilder)
    {
        _user = userBuilder.Build();
        return this;
    }

    public HttpContextBuilder WithItem(string key, object value)
    {
        _items[key] = value;
        return this;
    }

    protected override HttpContext BuildInternal()
    {
        var context = new DefaultHttpContext();

        if (_user != null) context.User = _user;

        foreach (KeyValuePair<string, object> item in _items) context.Items[item.Key] = item.Value;

        return context;
    }
}

/// <summary>
///     Static factory class for common test data scenarios
/// </summary>
public static class TestData
{
    /// <summary>
    ///     Creates a standard authenticated user for testing
    /// </summary>
    public static ClaimsPrincipal CreateAuthenticatedUser()
    {
        return ClaimsPrincipalBuilder.Create()
            .WithPersonGuid(Guids.PersonGuid1)
            .WithTenantGuid(Guids.TenantGuid1)
            .WithUserId(Strings.TestUserId)
            .WithEmail(Strings.TestEmail)
            .Build();
    }

    /// <summary>
    ///     Creates an HttpContext with an authenticated user
    /// </summary>
    public static HttpContext CreateAuthenticatedHttpContext()
    {
        return HttpContextBuilder.Create()
            .WithUser(CreateAuthenticatedUser())
            .Build();
    }

    /// <summary>
    ///     Common test GUIDs for consistent testing
    /// </summary>
    public static class Guids
    {
        public static readonly Guid PersonGuid1 = new("123e4567-e89b-12d3-a456-426614174001");
        public static readonly Guid PersonGuid2 = new("123e4567-e89b-12d3-a456-426614174002");
        public static readonly Guid TenantGuid1 = new("987fcdeb-51a2-43d6-b789-123456789001");
        public static readonly Guid TenantGuid2 = new("987fcdeb-51a2-43d6-b789-123456789002");
        public static readonly Guid Empty = Guid.Empty;
    }

    /// <summary>
    ///     Common test patient IDs
    /// </summary>
    public static class PatientIds
    {
        public const long PatientId1 = 12345L;
        public const long PatientId2 = 67890L;
        public const long MaxValue = long.MaxValue / 2;
        public const long MinValue = 0L;
    }

    /// <summary>
    ///     Common test strings
    /// </summary>
    public static class Strings
    {
        public const string TestEmail = "test@example.com";
        public const string TestUserId = "test-user-123";
        public const string TestFileName = "test-file.txt";
        public const string TestBucketName = "test-bucket";

        public const string TestQueueUrl
            = "https://sqs.us-east-1.amazonaws.com/123456789012/test-queue";
    }
}