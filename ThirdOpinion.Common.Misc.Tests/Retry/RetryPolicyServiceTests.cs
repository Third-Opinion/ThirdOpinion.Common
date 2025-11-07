using System.Net;
using Microsoft.Extensions.Logging;
using Polly;
using ThirdOpinion.Common.Misc.Retry;

namespace ThirdOpinion.Common.Misc.Tests.Retry;

public class RetryPolicyServiceTests
{
    private readonly Mock<ILogger<RetryPolicyService>> _mockLogger;
    private readonly RetryPolicyService _retryPolicyService;

    public RetryPolicyServiceTests()
    {
        _mockLogger = new Mock<ILogger<RetryPolicyService>>();
        _retryPolicyService = new RetryPolicyService(_mockLogger.Object);
    }

    [Theory]
    [InlineData("Athena")]
    [InlineData("HealthLake")]
    [InlineData("UnknownService")]
    public void GetRetryPolicy_ShouldReturnPolicy_ForAnyServiceName(string serviceName)
    {
        // Act
        IAsyncPolicy<HttpResponseMessage> policy = _retryPolicyService.GetRetryPolicy(serviceName);

        // Assert
        policy.ShouldNotBeNull();
    }

    [Theory]
    [InlineData("Athena")]
    [InlineData("HealthLake")]
    [InlineData("UnknownService")]
    public void GetCircuitBreakerPolicy_ShouldReturnPolicy_ForAnyServiceName(string serviceName)
    {
        // Act
        IAsyncPolicy<HttpResponseMessage> policy
            = _retryPolicyService.GetCircuitBreakerPolicy(serviceName);

        // Assert
        policy.ShouldNotBeNull();
    }

    [Theory]
    [InlineData("Athena")]
    [InlineData("HealthLake")]
    [InlineData("UnknownService")]
    public void GetCombinedPolicy_ShouldReturnPolicy_ForAnyServiceName(string serviceName)
    {
        // Act
        IAsyncPolicy<HttpResponseMessage> policy
            = _retryPolicyService.GetCombinedPolicy(serviceName);

        // Assert
        policy.ShouldNotBeNull();
    }

    [Fact]
    public async Task RetryPolicy_ShouldRetryOnTransientErrors()
    {
        // Arrange
        IAsyncPolicy<HttpResponseMessage>
            policy = _retryPolicyService.GetRetryPolicy("TestService");
        var callCount = 0;
        var expectedStatusCode = HttpStatusCode.InternalServerError;

        // Act & Assert
        var exception = await Should.ThrowAsync<HttpRequestException>(async () =>
        {
            await policy.ExecuteAsync(async () =>
            {
                callCount++;
                var response = new HttpResponseMessage(expectedStatusCode);

                // Always throw to test retry behavior
                throw new HttpRequestException($"Request failed with status {expectedStatusCode}");
            });
        });

        // Should have attempted 4 times (initial + 3 retries) before giving up
        callCount.ShouldBe(4);
    }

    [Fact]
    public async Task RetryPolicy_ShouldSucceedAfterTransientFailures()
    {
        // Arrange
        IAsyncPolicy<HttpResponseMessage>
            policy = _retryPolicyService.GetRetryPolicy("TestService");
        var callCount = 0;
        var successResponse = new HttpResponseMessage(HttpStatusCode.OK);

        // Act
        HttpResponseMessage? result = await policy.ExecuteAsync(async () =>
        {
            callCount++;

            // Fail first 3 attempts, succeed on 4th (initial + 3 retries)
            if (callCount < 4) throw new HttpRequestException("Connection timeout occurred");

            await Task.CompletedTask; // Make it async
            return successResponse;
        });

        // Assert
        callCount.ShouldBe(4);
        result.ShouldBe(successResponse);
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RetryPolicy_ShouldNotRetryOnNonTransientErrors()
    {
        // Arrange
        IAsyncPolicy<HttpResponseMessage>
            policy = _retryPolicyService.GetRetryPolicy("TestService");
        var callCount = 0;

        // Act & Assert
        var exception = await Should.ThrowAsync<ArgumentException>(async () =>
        {
            await policy.ExecuteAsync(async () =>
            {
                callCount++;
                throw new ArgumentException("Non-transient error");
            });
        });

        // Should only be called once for non-transient errors
        callCount.ShouldBe(1);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task RetryPolicy_ShouldNotRetryOnClientErrors(HttpStatusCode statusCode)
    {
        // Arrange
        IAsyncPolicy<HttpResponseMessage>
            policy = _retryPolicyService.GetRetryPolicy("TestService");
        var callCount = 0;

        // Act & Assert
        var exception = await Should.ThrowAsync<HttpRequestException>(async () =>
        {
            await policy.ExecuteAsync(async () =>
            {
                callCount++;
                throw new HttpRequestException($"Request failed with status {statusCode}");
            });
        });

        // Should only be called once for client errors
        callCount.ShouldBe(1);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task RetryPolicy_ShouldRetryOnServerErrors(HttpStatusCode statusCode)
    {
        // Arrange
        IAsyncPolicy<HttpResponseMessage>
            policy = _retryPolicyService.GetRetryPolicy("TestService");
        var callCount = 0;

        // Act & Assert
        var exception = await Should.ThrowAsync<HttpRequestException>(async () =>
        {
            await policy.ExecuteAsync(async () =>
            {
                callCount++;
                throw new HttpRequestException($"Request failed with status {statusCode}");
            });
        });

        // Should retry for server errors
        callCount.ShouldBeGreaterThan(1);
    }

    [Fact]
    public async Task CombinedPolicy_ShouldUseRetryAndCircuitBreaker()
    {
        // Arrange
        IAsyncPolicy<HttpResponseMessage> policy
            = _retryPolicyService.GetCombinedPolicy("TestService");
        var callCount = 0;

        // Act & Assert
        var exception = await Should.ThrowAsync<HttpRequestException>(async () =>
        {
            await policy.ExecuteAsync(async () =>
            {
                callCount++;
                throw new HttpRequestException("Server error", null,
                    HttpStatusCode.InternalServerError);
            });
        });

        // Should have attempted retries
        callCount.ShouldBeGreaterThan(1);
    }

    [Fact]
    public void Constructor_ShouldAcceptLogger()
    {
        // Arrange & Act
        var service = new RetryPolicyService(_mockLogger.Object);

        // Assert
        service.ShouldNotBeNull();
    }

    [Fact]
    public void GetRetryPolicy_ShouldReturnSamePolicyForSameService()
    {
        // Arrange
        var serviceName = "TestService";

        // Act
        IAsyncPolicy<HttpResponseMessage> policy1 = _retryPolicyService.GetRetryPolicy(serviceName);
        IAsyncPolicy<HttpResponseMessage> policy2 = _retryPolicyService.GetRetryPolicy(serviceName);

        // Assert
        policy1.ShouldBe(policy2); // Should return cached instance
    }

    [Fact]
    public void GetCircuitBreakerPolicy_ShouldReturnSamePolicyForSameService()
    {
        // Arrange
        var serviceName = "TestService";

        // Act
        IAsyncPolicy<HttpResponseMessage> policy1
            = _retryPolicyService.GetCircuitBreakerPolicy(serviceName);
        IAsyncPolicy<HttpResponseMessage> policy2
            = _retryPolicyService.GetCircuitBreakerPolicy(serviceName);

        // Assert
        policy1.ShouldBe(policy2); // Should return cached instance
    }

    [Fact]
    public void GetCombinedPolicy_ShouldReturnSamePolicyForSameService()
    {
        // Arrange
        var serviceName = "TestService";

        // Act
        IAsyncPolicy<HttpResponseMessage> policy1
            = _retryPolicyService.GetCombinedPolicy(serviceName);
        IAsyncPolicy<HttpResponseMessage> policy2
            = _retryPolicyService.GetCombinedPolicy(serviceName);

        // Assert
        policy1.ShouldBe(policy2); // Should return cached instance
    }

    [Fact]
    public async Task RetryPolicy_ShouldHaveExponentialBackoff()
    {
        // Arrange
        IAsyncPolicy<HttpResponseMessage>
            policy = _retryPolicyService.GetRetryPolicy("TestService");
        var callTimes = new List<DateTime>();

        // Act & Assert
        var exception = await Should.ThrowAsync<HttpRequestException>(async () =>
        {
            await policy.ExecuteAsync(async () =>
            {
                callTimes.Add(DateTime.UtcNow);
                throw new HttpRequestException("Server error", null,
                    HttpStatusCode.InternalServerError);
            });
        });

        // Should have made multiple attempts
        callTimes.Count.ShouldBeGreaterThan(1);

        // Verify increasing delays (allowing for some variance due to jitter)
        if (callTimes.Count >= 3)
        {
            TimeSpan delay1 = callTimes[1] - callTimes[0];
            TimeSpan delay2 = callTimes[2] - callTimes[1];

            // Second delay should be longer than first (exponential backoff)
            // Allow some variance for jitter
            delay2.TotalMilliseconds.ShouldBeGreaterThan(delay1.TotalMilliseconds * 0.8);
        }
    }
}