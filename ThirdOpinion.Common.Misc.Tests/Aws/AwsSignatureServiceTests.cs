using Amazon.Runtime;
using Microsoft.Extensions.Logging;
using ThirdOpinion.Common.Aws.Misc;

namespace ThirdOpinion.Common.Misc.Tests.Aws;

public class AwsSignatureServiceTests
{
    private readonly Mock<AWSCredentials> _credentialsMock;
    private readonly Mock<ILogger<AwsSignatureService>> _loggerMock;
    private readonly AwsSignatureService _service;

    public AwsSignatureServiceTests()
    {
        _loggerMock = new Mock<ILogger<AwsSignatureService>>();
        _credentialsMock = new Mock<AWSCredentials>();

        // Setup mock credentials to return test values
        var immutableCredentials = new ImmutableCredentials(
            "AKIAIOSFODNN7EXAMPLE",
            "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY",
            "AQoDYXdzEJr...<much longer token>..."
        );

        _credentialsMock
            .Setup(c => c.GetCredentialsAsync())
            .ReturnsAsync(immutableCredentials);

        _service = new AwsSignatureService(_loggerMock.Object, _credentialsMock.Object);
    }

    [Fact]
    public async Task SignRequestAsync_ShouldAddRequiredHeaders()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Put,
            "https://healthlake.us-east-1.amazonaws.com/datastore/test/r4/Patient/123");

        // Act
        var signedRequest = await _service.SignRequestAsync(request, "healthlake", "us-east-1");

        // Assert
        signedRequest.ShouldNotBeNull();
        signedRequest.Headers.Contains("X-Amz-Date").ShouldBeTrue();
        signedRequest.Headers.Contains("X-Amz-Security-Token").ShouldBeTrue();
        signedRequest.Headers.Contains("X-Amz-Content-Sha256").ShouldBeTrue();
        signedRequest.Headers.Contains("Authorization").ShouldBeTrue();
    }

    [Fact]
    public async Task SignRequestAsync_ShouldIncludeAuthorizationHeader()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Put,
            "https://healthlake.us-east-1.amazonaws.com/datastore/test/r4/Patient/123");

        // Act
        var signedRequest = await _service.SignRequestAsync(request, "healthlake", "us-east-1");

        // Assert - Authorization can be set as either typed header or raw header
        string authValue = null;

        if (signedRequest.Headers.Authorization != null)
            authValue = signedRequest.Headers.Authorization.ToString();
        else if (signedRequest.Headers.Contains("Authorization"))
            authValue = signedRequest.Headers.GetValues("Authorization").FirstOrDefault();

        authValue.ShouldNotBeNull();
        authValue.ShouldStartWith("AWS4-HMAC-SHA256");
        authValue.ShouldContain("Credential=AKIAIOSFODNN7EXAMPLE");
        authValue.ShouldContain("SignedHeaders=");
        authValue.ShouldContain("Signature=");
    }

    [Fact]
    public async Task SignRequestWithBodyAsync_ShouldHandleRequestBody()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Put,
            "https://healthlake.us-east-1.amazonaws.com/datastore/test/r4/Patient/123");
        var requestBody = "{\"resourceType\":\"Patient\",\"id\":\"123\"}";

        // Act
        var signedRequest
            = await _service.SignRequestWithBodyAsync(request, requestBody, "healthlake",
                "us-east-1");

        // Assert
        signedRequest.ShouldNotBeNull();
        signedRequest.Content.ShouldNotBeNull();

        var contentString = await signedRequest.Content.ReadAsStringAsync();
        contentString.ShouldBe(requestBody);

        // Content hash header should be based on the body
        var contentHash = signedRequest.Headers.GetValues("X-Amz-Content-Sha256").FirstOrDefault();
        contentHash.ShouldNotBeNull();
        contentHash.ShouldNotBe(
            "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"); // Empty string hash
    }

    [Fact]
    public async Task SignRequestAsync_ShouldHandleEmptyBody()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get,
            "https://healthlake.us-east-1.amazonaws.com/datastore/test/r4/Patient/123");

        // Act
        var signedRequest = await _service.SignRequestAsync(request, "healthlake", "us-east-1");

        // Assert
        // For GET requests, X-Amz-Content-Sha256 header is not added per AWS best practices
        signedRequest.Headers.Contains("X-Amz-Content-Sha256").ShouldBeFalse();

        // Verify other required headers are present
        signedRequest.Headers.Contains("X-Amz-Date").ShouldBeTrue();
        signedRequest.Headers.Contains("Authorization").ShouldBeTrue();
    }

    [Fact]
    public async Task SignRequestAsync_ShouldPreserveOriginalHeaders()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Put,
            "https://healthlake.us-east-1.amazonaws.com/datastore/test/r4/Patient/123");
        request.Headers.Add("X-Correlation-ID", "test-correlation-123");
        request.Headers.Add("User-Agent", "FhirTools/1.0");

        // Act
        var signedRequest = await _service.SignRequestAsync(request, "healthlake", "us-east-1");

        // Assert
        signedRequest.Headers.Contains("X-Correlation-ID").ShouldBeTrue();
        signedRequest.Headers.GetValues("X-Correlation-ID").First()
            .ShouldBe("test-correlation-123");
        signedRequest.Headers.Contains("User-Agent").ShouldBeTrue();
        signedRequest.Headers.GetValues("User-Agent").First().ShouldBe("FhirTools/1.0");
    }

    [Fact]
    public async Task SignRequestAsync_ShouldWorkWithDifferentRegions()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Put,
            "https://healthlake.eu-west-1.amazonaws.com/datastore/test/r4/Patient/123");

        // Act
        var signedRequest = await _service.SignRequestAsync(request, "healthlake", "eu-west-1");

        // Assert
        var authValue = signedRequest.Headers.GetValues("Authorization").FirstOrDefault();
        authValue.ShouldNotBeNull();
        authValue.ShouldContain("/eu-west-1/healthlake/aws4_request");
    }

    [Fact]
    public async Task SignRequestAsync_ShouldWorkWithDifferentServices()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get,
            "https://s3.us-east-1.amazonaws.com/mybucket/mykey");

        // Act
        var signedRequest = await _service.SignRequestAsync(request, "s3", "us-east-1");

        // Assert
        var authValue = signedRequest.Headers.GetValues("Authorization").FirstOrDefault();
        authValue.ShouldNotBeNull();
        authValue.ShouldContain("/us-east-1/s3/aws4_request");
    }

    [Fact]
    public async Task SignRequestAsync_ShouldHandleNullCredentials()
    {
        // Arrange
        var credentialsMock = new Mock<AWSCredentials>();
        credentialsMock
            .Setup(c => c.GetCredentialsAsync())
            .ThrowsAsync(new AmazonServiceException("Unable to retrieve credentials"));

        var service = new AwsSignatureService(_loggerMock.Object, credentialsMock.Object);
        var request = new HttpRequestMessage(HttpMethod.Get,
            "https://healthlake.us-east-1.amazonaws.com/datastore/test/r4/Patient");

        // Act & Assert
        await Should.ThrowAsync<AmazonServiceException>(async () =>
            await service.SignRequestAsync(request, "healthlake", "us-east-1"));
    }

    [Fact]
    public async Task SignRequestAsync_ShouldHandleCancellation()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Put,
            "https://healthlake.us-east-1.amazonaws.com/datastore/test/r4/Patient/123");
        var cts = new CancellationTokenSource();

        // Setup the credentials mock to throw when cancelled
        _credentialsMock
            .Setup(c => c.GetCredentialsAsync())
            .Returns(async () =>
            {
                await Task.Delay(100, cts.Token);
                return new ImmutableCredentials(
                    "AKIAIOSFODNN7EXAMPLE",
                    "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY",
                    "AQoDYXdzEJr...<much longer token>..."
                );
            });

        // Cancel the token
        cts.Cancel();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await _service.SignRequestAsync(request, "healthlake", "us-east-1", cts.Token));
    }
}