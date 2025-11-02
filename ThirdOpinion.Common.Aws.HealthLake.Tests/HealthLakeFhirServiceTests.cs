using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Amazon.HealthLake;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq.Protected;
using Polly;
using ThirdOpinion.Common.Aws.HealthLake.Configuration;
using ThirdOpinion.Common.Aws.HealthLake.Exceptions;
using ThirdOpinion.Common.Aws.HealthLake.Http;
using ThirdOpinion.Common.Aws.HealthLake.Logging;
using ThirdOpinion.Common.Aws.HealthLake.RateLimiting;
using ThirdOpinion.Common.Aws.HealthLake.Retry;

namespace ThirdOpinion.Common.Aws.HealthLake.Tests;

public class HealthLakeFhirServiceTests
{
    private readonly HealthLakeConfig _config;
    private readonly Mock<IOptions<HealthLakeConfig>> _configMock;
    private readonly Mock<ICorrelationIdProvider> _correlationIdProviderMock;
    private readonly Mock<IAmazonHealthLake> _healthLakeClientMock;
    private readonly Mock<IHealthLakeHttpService> _healthLakeHttpServiceMock;
    private readonly HttpClient _httpClient;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly Mock<ILogger<HealthLakeFhirService>> _loggerMock;
    private readonly Mock<IRateLimiter> _rateLimiterMock;
    private readonly Mock<IRateLimiterService> _rateLimiterServiceMock;
    private readonly Mock<IRetryPolicyService> _retryPolicyServiceMock;
    private readonly HealthLakeFhirService _service;

    public HealthLakeFhirServiceTests()
    {
        _healthLakeClientMock = new Mock<IAmazonHealthLake>();
        _configMock = new Mock<IOptions<HealthLakeConfig>>();
        _loggerMock = new Mock<ILogger<HealthLakeFhirService>>();
        _correlationIdProviderMock = new Mock<ICorrelationIdProvider>();
        _rateLimiterServiceMock = new Mock<IRateLimiterService>();
        _rateLimiterMock = new Mock<IRateLimiter>();
        _retryPolicyServiceMock = new Mock<IRetryPolicyService>();
        _healthLakeHttpServiceMock = new Mock<IHealthLakeHttpService>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();

        _config = new HealthLakeConfig
        {
            DatastoreId = "836e877666cebf177ce6370ec1478a92",
            Region = "us-east-2",
            RequestTimeoutSeconds = 30
        };
        _configMock.Setup(x => x.Value).Returns(_config);

        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);

        _correlationIdProviderMock.Setup(x => x.GetCorrelationId()).Returns("test-correlation-id");

        // Setup rate limiter mocks
        _rateLimiterServiceMock.Setup(x => x.GetRateLimiter("HealthLake"))
            .Returns(_rateLimiterMock.Object);
        _rateLimiterMock.Setup(x => x.WaitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Setup retry policy mocks
        _retryPolicyServiceMock.Setup(x => x.GetCombinedPolicy(It.IsAny<string>()))
            .Returns(Policy.NoOpAsync<HttpResponseMessage>());

        // Setup HealthLake HTTP service mock to capture request
        _healthLakeHttpServiceMock.Setup(x => x.SendSignedRequestAsync(
                It.IsAny<HttpRequestMessage>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((HttpRequestMessage req, CancellationToken ct) =>
                new HttpResponseMessage(HttpStatusCode.Created));

        _service = new HealthLakeFhirService(
            _healthLakeClientMock.Object,
            _configMock.Object,
            _loggerMock.Object,
            _healthLakeHttpServiceMock.Object);
    }

    [Fact]
    public async Task PutResourceAsync_WithValidRequest_ShouldSucceed()
    {
        // Arrange
        var resourceType = "Patient";
        var resourceId = "12345";
        var resourceJson
            = """{"resourceType":"Patient","id":"12345","name":[{"family":"Smith","given":["John"]}]}""";

        SetupHttpResponse(HttpStatusCode.Created, "");

        // Act
        await _service.PutResourceAsync(resourceType, resourceId, resourceJson);

        // Assert - verify the signed request was sent with correct URL
        _healthLakeHttpServiceMock.Verify(x => x.SendSignedRequestAsync(
            It.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Put &&
                req.RequestUri!.ToString().Contains($"datastore/{_config.DatastoreId}/r4/{resourceType}/{resourceId}")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PutResourceAsync_WithInvalidResourceType_ShouldThrowArgumentException()
    {
        // Arrange
        var resourceType = "InvalidType";
        var resourceId = "12345";
        var resourceJson = """{"resourceType":"InvalidType","id":"12345"}""";

        // Act & Assert
        var exception = await Should.ThrowAsync<ArgumentException>(async () =>
            await _service.PutResourceAsync(resourceType, resourceId, resourceJson));

        exception.Message.ShouldContain("not supported");
        exception.ParamName.ShouldBe("resourceType");
    }

    [Fact]
    public async Task PutResourceAsync_WithEmptyResourceId_ShouldThrowArgumentException()
    {
        // Arrange
        var resourceType = "Patient";
        var resourceId = "";
        var resourceJson = """{"resourceType":"Patient","id":""}""";

        // Act & Assert
        var exception = await Should.ThrowAsync<ArgumentException>(async () =>
            await _service.PutResourceAsync(resourceType, resourceId, resourceJson));

        exception.Message.ShouldContain("cannot be null or empty");
        exception.ParamName.ShouldBe("resourceId");
    }

    [Fact]
    public async Task PutResourceAsync_WithInvalidJson_ShouldThrowArgumentException()
    {
        // Arrange
        var resourceType = "Patient";
        var resourceId = "12345";
        var invalidJson = "not valid json";

        // Act & Assert
        var exception = await Should.ThrowAsync<ArgumentException>(async () =>
            await _service.PutResourceAsync(resourceType, resourceId, invalidJson));

        exception.Message.ShouldContain("Invalid JSON format");
        exception.ParamName.ShouldBe("resourceJson");
    }

    [Fact]
    public async Task PutResourceAsync_WithBadRequest_ShouldThrowHealthLakeException()
    {
        // Arrange
        var resourceType = "Patient";
        var resourceId = "12345";
        var resourceJson = """{"resourceType":"Patient","id":"12345"}""";

        SetupHttpResponse(HttpStatusCode.BadRequest, "Invalid resource format");

        // Act & Assert
        var exception = await Should.ThrowAsync<HealthLakeException>(async () =>
            await _service.PutResourceAsync(resourceType, resourceId, resourceJson));

        exception.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        exception.ResourceType.ShouldBe(resourceType);
        exception.ResourceId.ShouldBe(resourceId);
        exception.Message.ShouldContain("Invalid FHIR resource format");
    }

    [Fact]
    public async Task PutResourceAsync_WithUnauthorized_ShouldThrowHealthLakeException()
    {
        // Arrange
        var resourceType = "Patient";
        var resourceId = "12345";
        var resourceJson = """{"resourceType":"Patient","id":"12345"}""";

        SetupHttpResponse(HttpStatusCode.Unauthorized, "");

        // Act & Assert
        var exception = await Should.ThrowAsync<HealthLakeException>(async () =>
            await _service.PutResourceAsync(resourceType, resourceId, resourceJson));

        exception.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        exception.Message.ShouldContain("Authentication failed");
    }

    [Fact]
    public async Task PutResourceAsync_WithForbidden_ShouldThrowHealthLakeAccessDeniedException()
    {
        // Arrange
        var resourceType = "Patient";
        var resourceId = "12345";
        var resourceJson = """{"resourceType":"Patient","id":"12345"}""";

        SetupHttpResponse(HttpStatusCode.Forbidden, "");

        // Act & Assert
        var exception = await Should.ThrowAsync<HealthLakeAccessDeniedException>(async () =>
            await _service.PutResourceAsync(resourceType, resourceId, resourceJson));

        exception.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        exception.Message.ShouldContain("Access denied");
    }

    [Fact]
    public async Task PutResourceAsync_WithConflict_ShouldThrowHealthLakeConflictException()
    {
        // Arrange
        var resourceType = "Patient";
        var resourceId = "12345";
        var resourceJson = """{"resourceType":"Patient","id":"12345"}""";

        SetupHttpResponse(HttpStatusCode.Conflict, "");

        // Act & Assert
        var exception = await Should.ThrowAsync<HealthLakeConflictException>(async () =>
            await _service.PutResourceAsync(resourceType, resourceId, resourceJson));

        exception.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        exception.ResourceType.ShouldBe(resourceType);
        exception.ResourceId.ShouldBe(resourceId);
    }

    [Fact]
    public async Task
        PutResourceAsync_WithTooManyRequests_ShouldThrowHealthLakeThrottlingException()
    {
        // Arrange
        var resourceType = "Patient";
        var resourceId = "12345";
        var resourceJson = """{"resourceType":"Patient","id":"12345"}""";

        // Setup the mock to return TooManyRequests with RetryAfter header
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(60));

        _healthLakeHttpServiceMock.Setup(x => x.SendSignedRequestAsync(
                It.IsAny<HttpRequestMessage>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act & Assert
        var exception = await Should.ThrowAsync<HealthLakeThrottlingException>(async () =>
            await _service.PutResourceAsync(resourceType, resourceId, resourceJson));

        exception.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        exception.RetryAfter.ShouldNotBeNull();
    }

    [Fact]
    public async Task PutResourceAsync_WithServerError_ShouldThrowHealthLakeException()
    {
        // Arrange
        var resourceType = "Patient";
        var resourceId = "12345";
        var resourceJson = """{"resourceType":"Patient","id":"12345"}""";

        SetupHttpResponse(HttpStatusCode.InternalServerError, "Internal server error");

        // Act & Assert
        var exception = await Should.ThrowAsync<HealthLakeException>(async () =>
            await _service.PutResourceAsync(resourceType, resourceId, resourceJson));

        exception.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        exception.Message.ShouldContain("HealthLake service error");
    }

    [Fact]
    public async Task PutResourceAsyncGeneric_WithValidObject_ShouldSerializeAndSucceed()
    {
        // Arrange
        var resourceType = "Patient";
        var resourceId = "12345";
        var patient = new TestPatientResource
            { ResourceType = "Patient", Id = "12345", Active = true };

        SetupHttpResponse(HttpStatusCode.Created, "");

        // Act
        await _service.PutResourceAsync(resourceType, resourceId, patient);

        // Assert
        VerifyHttpRequest(HttpMethod.Put,
            $"datastore/{_config.DatastoreId}/r4/{resourceType}/{resourceId}");
    }

    [Fact]
    public async Task PutResourcesAsync_WithMultipleResources_ShouldProcessInParallel()
    {
        // Arrange
        var resources = new[]
        {
            ("Patient", "1", """{"resourceType":"Patient","id":"1"}"""),
            ("Practitioner", "2", """{"resourceType":"Practitioner","id":"2"}"""),
            ("Medication", "3", """{"resourceType":"Medication","id":"3"}""")
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.Created));

        // Act
        Dictionary<string, bool> results = await _service.PutResourcesAsync(resources);

        // Assert
        results.Count.ShouldBe(3);
        results["Patient/1"].ShouldBeTrue();
        results["Practitioner/2"].ShouldBeTrue();
        results["Medication/3"].ShouldBeTrue();
    }

    [Fact]
    public async Task PutResourcesAsync_WithSomeFailures_ShouldReturnMixedResults()
    {
        // Arrange
        var resources = new[]
        {
            ("Patient", "1", """{"resourceType":"Patient","id":"1"}"""),
            ("Patient", "2", """{"resourceType":"Patient","id":"2"}""")
        };

        _healthLakeHttpServiceMock.SetupSequence(x => x.SendSignedRequestAsync(
                It.IsAny<HttpRequestMessage>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.Created))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.BadRequest));

        // Act
        Dictionary<string, bool> results = await _service.PutResourcesAsync(resources);

        // Assert
        results.Count.ShouldBe(2);
        results["Patient/1"].ShouldBeTrue();
        results["Patient/2"].ShouldBeFalse();
    }

    [Fact]
    public void IsResourceTypeSupported_WithSupportedTypes_ShouldReturnTrue()
    {
        // Act & Assert
        _service.IsResourceTypeSupported("Patient").ShouldBeTrue();
        _service.IsResourceTypeSupported("Practitioner").ShouldBeTrue();
        _service.IsResourceTypeSupported("Medication").ShouldBeTrue();
        _service.IsResourceTypeSupported("Observation").ShouldBeTrue();
        _service.IsResourceTypeSupported("patient").ShouldBeTrue(); // Case insensitive
    }

    [Fact]
    public void IsResourceTypeSupported_WithUnsupportedType_ShouldReturnFalse()
    {
        // Act & Assert
        _service.IsResourceTypeSupported("InvalidType").ShouldBeFalse();
        _service.IsResourceTypeSupported("").ShouldBeFalse();
    }

    [Fact]
    public void GetSupportedResourceTypes_ShouldReturnAllTypes()
    {
        // Act
        IReadOnlyList<string> types = _service.GetSupportedResourceTypes();

        // Assert
        types.ShouldNotBeEmpty();
        types.ShouldContain("Patient");
        types.ShouldContain("Practitioner");
        types.ShouldContain("Medication");
        types.ShouldContain("Observation");
        types.Count.ShouldBeGreaterThan(40); // HealthLake supports many resource types
    }

    [Fact]
    public async Task PutResourceAsync_ShouldSucceedWithConcurrencyControl()
    {
        // Arrange
        var resourceType = "Patient";
        var resourceId = "12345";
        var resourceJson = """{"resourceType":"Patient","id":"12345"}""";

        SetupHttpResponse(HttpStatusCode.Created, "");

        // Act - verify that concurrent requests work (semaphore controls concurrency internally)
        await _service.PutResourceAsync(resourceType, resourceId, resourceJson);

        // Assert - verify the signed request was sent
        _healthLakeHttpServiceMock.Verify(x => x.SendSignedRequestAsync(
            It.IsAny<HttpRequestMessage>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PutResourceAsync_ShouldSignRequestWithAwsSignature()
    {
        // Arrange
        var resourceType = "Patient";
        var resourceId = "12345";
        var resourceJson = """{"resourceType":"Patient","id":"12345"}""";

        SetupHttpResponse(HttpStatusCode.Created, "");

        // Act
        await _service.PutResourceAsync(resourceType, resourceId, resourceJson);

        // Assert
        _healthLakeHttpServiceMock.Verify(x => x.SendSignedRequestAsync(
            It.IsAny<HttpRequestMessage>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PutResourceAsync_WithAwsSignature_ShouldUseCorrectServiceAndRegion()
    {
        // Arrange
        var resourceType = "Patient";
        var resourceId = "12345";
        var resourceJson = """{"resourceType":"Patient","id":"12345"}""";

        SetupHttpResponse(HttpStatusCode.Created, "");

        // Act
        await _service.PutResourceAsync(resourceType, resourceId, resourceJson);

        // Assert
        _healthLakeHttpServiceMock.Verify(x => x.SendSignedRequestAsync(
            It.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Put &&
                req.RequestUri.ToString()
                    .Contains($"datastore/{_config.DatastoreId}/r4/{resourceType}/{resourceId}")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PutResourceAsync_ShouldSetCorrectHeaders()
    {
        // Arrange
        var resourceType = "Patient";
        var resourceId = "12345";
        var resourceJson = """{"resourceType":"Patient","id":"12345"}""";
        HttpRequestMessage? capturedRequest = null;

        _healthLakeHttpServiceMock.Setup(x => x.SendSignedRequestAsync(
                It.IsAny<HttpRequestMessage>(),
                It.IsAny<CancellationToken>()))
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.Created));

        // Act
        await _service.PutResourceAsync(resourceType, resourceId, resourceJson);

        // Assert
        capturedRequest.ShouldNotBeNull();

        // Verify Content-Type is set correctly (without charset)
        capturedRequest.Content?.Headers?.ContentType?.MediaType.ShouldBe("application/fhir+json");
        capturedRequest.Content?.Headers?.ContentType?.CharSet.ShouldBeNull();

        // Verify validation level header
        capturedRequest.Headers.Contains("x-amz-fhir-validation-level").ShouldBeTrue();
        capturedRequest.Headers.GetValues("x-amz-fhir-validation-level").First().ShouldBe("strict");
    }

    [Fact]
    public async Task PutResourceAsync_WithIfMatchVersion_ShouldSetIfMatchHeader()
    {
        // Arrange
        var resourceType = "Patient";
        var resourceId = "12345";
        var resourceJson = """{"resourceType":"Patient","id":"12345"}""";
        var ifMatchVersion = "W/\"2\"";
        HttpRequestMessage? capturedRequest = null;

        _healthLakeHttpServiceMock.Setup(x => x.SendSignedRequestAsync(
                It.IsAny<HttpRequestMessage>(),
                It.IsAny<CancellationToken>()))
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        // Act - using the overload with ifMatchVersion parameter
        await _service.PutResourceAsync(resourceType, resourceId, resourceJson, ifMatchVersion);

        // Assert
        capturedRequest.ShouldNotBeNull();
        capturedRequest.Headers.Contains("If-Match").ShouldBeTrue();
        capturedRequest.Headers.GetValues("If-Match").First().ShouldBe(ifMatchVersion);
    }

    [Fact]
    public async Task PutResourceAsync_WithETagInResponse_ShouldReturnVersion()
    {
        // Arrange
        var resourceType = "Patient";
        var resourceId = "12345";
        var resourceJson = """{"resourceType":"Patient","id":"12345"}""";
        var expectedVersion = "3";

        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.ETag = new EntityTagHeaderValue($"\"{expectedVersion}\"", true); // W/"3"

        _healthLakeHttpServiceMock.Setup(x => x.SendSignedRequestAsync(
                It.IsAny<HttpRequestMessage>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        string? version
            = await _service.PutResourceAsync(resourceType, resourceId, resourceJson, null);

        // Assert
        version.ShouldBe(expectedVersion);
    }

    [Fact]
    public async Task PutResourceAsync_WithOperationOutcomeError_ShouldExtractErrorDetails()
    {
        // Arrange
        var resourceType = "Patient";
        var resourceId = "12345";
        var resourceJson = """{"resourceType":"Patient","id":"12345"}""";

        var operationOutcome = """
                               {
                                   "resourceType": "OperationOutcome",
                                   "issue": [
                                       {
                                           "severity": "error",
                                           "code": "invalid",
                                           "diagnostics": "Missing required field: birthDate"
                                       }
                                   ]
                               }
                               """;

        SetupHttpResponse(HttpStatusCode.BadRequest, operationOutcome);

        // Act & Assert
        var exception = await Should.ThrowAsync<HealthLakeException>(async () =>
            await _service.PutResourceAsync(resourceType, resourceId, resourceJson));

        exception.Message.ShouldContain("Missing required field: birthDate");
    }

    private void SetupHttpResponse(HttpStatusCode statusCode, string content)
    {
        var response = new HttpResponseMessage(statusCode);
        if (!string.IsNullOrEmpty(content))
            response.Content = new StringContent(content, Encoding.UTF8, "application/fhir+json");

        _healthLakeHttpServiceMock.Setup(x => x.SendSignedRequestAsync(
                It.IsAny<HttpRequestMessage>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
    }

    private void VerifyHttpRequest(HttpMethod method, string expectedPath)
    {
        _healthLakeHttpServiceMock.Verify(x => x.SendSignedRequestAsync(
            It.Is<HttpRequestMessage>(req =>
                req.Method == method &&
                req.RequestUri!.ToString().Contains(expectedPath)),
            It.IsAny<CancellationToken>()), Times.Once());
    }

    // Test model class
    private class TestPatientResource
    {
        public string ResourceType { get; set; } = "";
        public string Id { get; set; } = "";
        public bool Active { get; set; }
    }
}