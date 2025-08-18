using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using ThirdOpinion.Common.TestApi.Models;

namespace ThirdOpinion.Common.TestApi.Tests;

public class AwsTestControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    
    public AwsTestControllerIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }
    
    [Fact]
    public async Task GetS3Test_ReturnsOkResult()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/aws-test/s3/test");
        
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<TestSuiteResult>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        
        result.ShouldNotBeNull();
        result.ServiceName.ShouldBe("S3");
        result.Results.ShouldNotBeEmpty();
    }
    
    [Fact]
    public async Task GetDynamoDBTest_ReturnsOkResult()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/aws-test/dynamodb/test");
        
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<TestSuiteResult>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        
        result.ShouldNotBeNull();
        result.ServiceName.ShouldBe("DynamoDB");
        result.Results.ShouldNotBeEmpty();
    }
    
    [Fact]
    public async Task GetSQSTest_ReturnsOkResult()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/aws-test/sqs/test");
        
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<TestSuiteResult>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        
        result.ShouldNotBeNull();
        result.ServiceName.ShouldBe("SQS");
        result.Results.ShouldNotBeEmpty();
    }
    
    [Fact]
    public async Task GetCognitoTest_ReturnsOkResult()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/aws-test/cognito/test");
        
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<TestSuiteResult>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        
        result.ShouldNotBeNull();
        result.ServiceName.ShouldBe("Cognito");
        result.Results.ShouldNotBeEmpty();
    }
    
    [Fact]
    public async Task GetAllTests_ReturnsOkResult()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/aws-test/test-all");
        
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var results = JsonSerializer.Deserialize<Dictionary<string, TestSuiteResult>>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        
        results.ShouldNotBeNull();
        results.ShouldContainKey("S3");
        results.ShouldContainKey("DynamoDB");
        results.ShouldContainKey("SQS");
        results.ShouldContainKey("Cognito");
        
        foreach (var kvp in results)
        {
            kvp.Value.ServiceName.ShouldBe(kvp.Key);
            kvp.Value.Results.ShouldNotBeEmpty();
        }
    }
}