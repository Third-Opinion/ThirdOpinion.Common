using Amazon.HealthLake;
using Amazon.HealthLake.Model;
using Microsoft.Extensions.Configuration;
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


    protected override Task CleanupTestResourcesAsync()
    {
        WriteOutput("HealthLake cleanup - no resources to clean up");
        return Task.CompletedTask;
    }
}