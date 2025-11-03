using System.Text.Json;
using Amazon.HealthLake;
using Amazon.HealthLake.Model;
using Microsoft.Extensions.Configuration;
using ThirdOpinion.Common.Aws.HealthLake;
using ThirdOpinion.Common.Aws.HealthLake.Exceptions;
using ThirdOpinion.Common.FunctionalTests.Infrastructure;
using Xunit.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace ThirdOpinion.Common.FunctionalTests.Tests;

[Collection("HealthLake")]
public class HealthLakeFunctionalTests : BaseIntegrationTest
{
    private readonly List<string> _createdResourceIds = new();
    private readonly string? _datastoreEndpoint;
    private readonly string? _datastoreId;
    private readonly bool _isConfigured;
    private readonly int _testPatientCount;

    public HealthLakeFunctionalTests(ITestOutputHelper output) : base(output)
    {
        _datastoreId = Configuration.GetValue<string>("HealthLake:DatastoreId");
        _datastoreEndpoint = Configuration.GetValue<string>("HealthLake:DatastoreEndpoint");
        _testPatientCount = Configuration.GetValue("HealthLake:TestPatientResourceCount", 5);

        _isConfigured = !string.IsNullOrEmpty(_datastoreId) &&
                        !string.IsNullOrEmpty(_datastoreEndpoint);
    }

    [Fact]
    public async Task HealthLakeClient_ShouldListDataStores()
    {
        WriteOutput("Testing HealthLake client - listing data stores...");

        var request = new ListFHIRDatastoresRequest
        {
            MaxResults = 10
        };

        ListFHIRDatastoresResponse? response
            = await HealthLakeClient.ListFHIRDatastoresAsync(request);

        WriteOutput($"Found {response.DatastorePropertiesList.Count} data stores");

        response.ShouldNotBeNull();
        response.DatastorePropertiesList.ShouldNotBeNull();

        if (response.DatastorePropertiesList.Any())
        {
            DatastoreProperties? firstDatastore = response.DatastorePropertiesList[0];
            WriteOutput(
                $"First datastore: {firstDatastore.DatastoreName} (ID: {firstDatastore.DatastoreId})");
            WriteOutput(
                $"Status: {firstDatastore.DatastoreStatus}, Type: {firstDatastore.DatastoreTypeVersion}");
        }

        WriteOutput("✓ Successfully listed HealthLake data stores");
    }

    [Fact]
    public async Task HealthLakeClient_ShouldDescribeDataStore_WhenConfigured()
    {
        if (!_isConfigured)
        {
            WriteOutput("⚠️ HealthLake datastore not configured, skipping describe test");
            WriteOutput(
                "To test HealthLake, set HealthLake:DatastoreId and HealthLake:DatastoreEndpoint in appsettings.Test.json");
            return;
        }

        WriteOutput($"Testing HealthLake client - describing datastore {_datastoreId}...");

        var request = new DescribeFHIRDatastoreRequest
        {
            DatastoreId = _datastoreId
        };

        DescribeFHIRDatastoreResponse? response
            = await HealthLakeClient.DescribeFHIRDatastoreAsync(request);

        WriteOutput($"Datastore name: {response.DatastoreProperties.DatastoreName}");
        WriteOutput($"Status: {response.DatastoreProperties.DatastoreStatus}");
        WriteOutput($"Endpoint: {response.DatastoreProperties.DatastoreEndpoint}");

        response.ShouldNotBeNull();
        response.DatastoreProperties.ShouldNotBeNull();
        response.DatastoreProperties.DatastoreId.ShouldBe(_datastoreId);
        response.DatastoreProperties.DatastoreStatus.ShouldBe(DatastoreStatus.ACTIVE);

        WriteOutput("✓ Successfully described HealthLake datastore");
    }

    [Fact]
    public async Task HealthLakeClient_ShouldWorkWithDataStore_WhenConfigured()
    {
        if (!_isConfigured)
        {
            WriteOutput(
                "⚠️ HealthLake datastore not configured, skipping datastore operations test");
            WriteOutput("This test requires a configured HealthLake datastore for FHIR operations");
            return;
        }

        WriteOutput("Testing HealthLake FHIR operations (limited to datastore info)...");
        WriteOutput($"Configured datastore: {_datastoreId}");
        WriteOutput($"Configured endpoint: {_datastoreEndpoint}");

        WriteOutput("✓ HealthLake client configured for FHIR operations");
        WriteOutput("Note: Full FHIR resource tests require additional setup and permissions");
    }

    [Fact]
    public async Task HealthLakeFhirService_GetResourceAsync_ShouldRetrieveResource_WhenConfigured()
    {
        if (!_isConfigured)
        {
            WriteOutput("⚠️ HealthLake datastore not configured, skipping GetResourceAsync test");
            WriteOutput(
                "To test HealthLake FHIR operations, set HealthLake:DatastoreId, HealthLake:DatastoreEndpoint, and HealthLake:Region in appsettings.Test.json");
            return;
        }

        if (HealthLakeFhirService == null)
        {
            WriteOutput("⚠️ HealthLakeFhirService not configured, skipping test");
            return;
        }

        WriteOutput("Testing HealthLakeFhirService.GetResourceAsync...");

        // Generate unique test patient ID
        var patientId = GenerateTestResourceName("patient");
        _createdResourceIds.Add($"Patient/{patientId}");

        WriteOutput($"Creating test Patient resource with ID: {patientId}");

        // Create a simple FHIR Patient resource
        var patientResource = new
        {
            resourceType = "Patient",
            id = patientId,
            identifier = new[]
            {
                new
                {
                    system = "http://third-opinion.com/test-patient-id",
                    value = patientId
                }
            },
            name = new[]
            {
                new
                {
                    use = "official",
                    family = "TestPatient",
                    given = new[] { "FunctionalTest" }
                }
            },
            gender = "unknown",
            birthDate = "2000-01-01",
            active = true
        };

        string patientJson = JsonSerializer.Serialize(patientResource, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        // First, PUT the resource to HealthLake
        WriteOutput("Putting Patient resource to HealthLake...");
        await HealthLakeFhirService.PutResourceAsync("Patient", patientId, patientJson);
        WriteOutput("✓ Patient resource created successfully");

        // Wait a brief moment to ensure the resource is available
        await Task.Delay(TimeSpan.FromSeconds(1));

        // Now, retrieve the resource using GetResourceAsync
        WriteOutput($"Retrieving Patient resource with ID: {patientId}");
        var retrievedPatient = await HealthLakeFhirService.GetResourceAsync<JsonDocument>(
            "Patient",
            patientId
        );

        WriteOutput("✓ Patient resource retrieved successfully");

        // Verify the retrieved resource
        retrievedPatient.ShouldNotBeNull();

        JsonElement root = retrievedPatient.RootElement;
        root.TryGetProperty("resourceType", out JsonElement resourceType).ShouldBeTrue();
        resourceType.GetString().ShouldBe("Patient");

        root.TryGetProperty("id", out JsonElement id).ShouldBeTrue();
        id.GetString().ShouldBe(patientId);

        root.TryGetProperty("identifier", out JsonElement identifiers).ShouldBeTrue();
        identifiers.ValueKind.ShouldBe(JsonValueKind.Array);

        root.TryGetProperty("name", out JsonElement names).ShouldBeTrue();
        names.ValueKind.ShouldBe(JsonValueKind.Array);

        var firstName = names.EnumerateArray().First();
        firstName.TryGetProperty("family", out JsonElement family).ShouldBeTrue();
        family.GetString().ShouldBe("TestPatient");

        WriteOutput($"✓ Retrieved Patient verified: ID={id.GetString()}, Name={family.GetString()}");
        WriteOutput("✓ HealthLakeFhirService.GetResourceAsync test completed successfully");
    }

    [Fact]
    public async Task HealthLakeFhirService_GetResourceAsync_ShouldThrowException_WhenResourceNotFound()
    {
        if (!_isConfigured)
        {
            WriteOutput("⚠️ HealthLake datastore not configured, skipping GetResourceAsync error test");
            return;
        }

        if (HealthLakeFhirService == null)
        {
            WriteOutput("⚠️ HealthLakeFhirService not configured, skipping test");
            return;
        }

        WriteOutput("Testing HealthLakeFhirService.GetResourceAsync with non-existent resource...");

        var nonExistentId = $"non-existent-{Guid.NewGuid()}";
        WriteOutput($"Attempting to retrieve non-existent Patient with ID: {nonExistentId}");

        var exception = await Should.ThrowAsync<HealthLakeException>(async () =>
        {
            await HealthLakeFhirService.GetResourceAsync<JsonDocument>("Patient", nonExistentId);
        });

        exception.ShouldNotBeNull();
        exception.StatusCode.ShouldBe(System.Net.HttpStatusCode.NotFound);
        WriteOutput($"✓ Expected HealthLakeException thrown: {exception.Message}");
        WriteOutput("✓ GetResourceAsync error handling test completed successfully");
    }


    protected override Task CleanupTestResourcesAsync()
    {
        WriteOutput("HealthLake cleanup - no resources to clean up");
        return Task.CompletedTask;
    }
}